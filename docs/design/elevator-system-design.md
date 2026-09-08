# Elevator System Design — Architecture & Design Document

**Status:** Approved · **Date:** 2026-09-08 · **Scope:** Easy Level (single elevator, floors 1–10)

---

## 1. Context

This document describes the design of an elevator control system that accepts passenger
requests, schedules them, and drives a single elevator car through its state machine. It is the
reference every implementation pull request is measured against.

The design deliberately targets the **Easy Level** of the brief — one elevator, floors 1 to 10,
FIFO scheduling — and treats the harder levels as explicit non-goals. Every extension point that
exists is there because it costs nothing today, not because a future level was speculatively
built.

## 2. Requirements

### 2.1 Functional

| ID | Requirement | Where it is satisfied |
|----|-------------|-----------------------|
| F1 | An `Elevator` class represents a single elevator | `ElevatorSystem.Core.Elevator` |
| F2 | An `ElevatorController` class manages elevator operations | `ElevatorSystem.Core.ElevatorController` |
| F3 | Handle pickup requests (floor + direction) | `ElevatorController.RequestElevator(floor, direction)` |
| F4 | Handle destination requests | `ElevatorController.RequestDestination(floor)` |
| F5 | States: `Idle`, `MovingUp`, `MovingDown`, `DoorOpen` | `ElevatorState` |
| F6 | Movement and door primitives | `MoveUp`, `MoveDown`, `OpenDoor`, `CloseDoor` |
| F7 | Queue management for floor requests | `Elevator.AddRequest` + `TargetFloors` |
| F8 | Simple FIFO scheduling algorithm | `FifoSchedulingStrategy` |
| F9 | Simulation of elevator movement | `ElevatorRunner` + `SimulationHostedService` (simulator) |
| F10 | Simple logging of elevator actions | `IElevatorEventSink` + `LoggingElevatorEventSink` (simulator) |

### 2.2 Non-functional

| ID | Requirement | How it is met |
|----|-------------|---------------|
| N1 | Thread-safe operations for concurrent requests | Multi-producer / single-consumer pipeline (§5) |
| N2 | Atomic state changes | Immutable snapshot published as one atomic reference write (§5) |
| N3 | No race conditions in request assignment | The consumer is serialised by a gate, so assignment cannot interleave (§5) |
| N4 | Thread-safe collections | `ConcurrentQueue<T>` for ingress |
| N5 | 100+ concurrent requests handled efficiently | Lock-free enqueue; verified by concurrency tests (§8) |
| N6 | Elevator assignment under 100 ms | Ingress is O(1) and never blocks; verified by a latency test (§8) |
| N7 | Reasonable memory under load | Bounded queue growth; requests are small immutable records |
| N8 | Invalid floor requests handled gracefully | Rejected at the boundary as a result, not an exception (§7) |
| N9 | Timeouts for stuck elevators | `StuckElevatorWatchdog` plus withdrawal from service (§7) |
| N10 | Exception handling for concurrent operations | Per-request isolation in the processing loop (§7) |

### 2.3 Non-goals

Multiple elevators and inter-car assignment; SCAN/LOOK or destination-dispatch scheduling;
capacity and weight limits; persistence; a network API; a graphical UI. Each is out of scope for
the Easy Level and is called out here so their absence reads as a decision rather than an
omission.

## 3. Solution structure

```text
ElevatorSystem.sln
├── src/
│   ├── ElevatorSystem.Core/          # pure domain, no third-party dependencies
│   └── ElevatorSystem.Simulator/     # console host: DI, logging, scenarios
├── tests/
│   ├── ElevatorSystem.Core.UnitTests/
│   ├── ElevatorSystem.Simulator.Tests/
│   └── ElevatorSystem.Core.ConcurrencyTests/
├── docs/
│   ├── design/                       # this document
│   └── adr/                          # one record per architectural decision
└── .github/workflows/ci.yml
```

`ElevatorSystem.Core` references nothing outside the BCL. It knows nothing about consoles,
threads-as-infrastructure, or logging frameworks. That constraint is what keeps the domain
testable without ceremony, and it is enforced by review rather than by tooling.

**Target framework:** `net10.0`, pinned via `global.json` so any reviewer reproduces the exact
build.

## 4. Component design

| Component | Single responsibility |
|-----------|----------------------|
| `ElevatorState`, `Direction` | The vocabulary of the domain |
| `PickupRequest`, `DestinationRequest` | Immutable request records |
| `FloorRange` | The one place that knows which floors exist |
| `Elevator` | The state machine of one car; exposes the brief's members plus a deterministic `Step()` |
| `IElevatorSchedulingStrategy` | Chooses the next target floor |
| `FifoSchedulingStrategy` | First-in, first-out implementation of the above |
| `ElevatorController` | Thread-safe public boundary: validate, enqueue, process, expose state |
| `RequestResult` | The outcome of an admission attempt, reported as a value |
| `ElevatorSnapshot` | An immutable description of the system at one instant |
| `StuckElevatorWatchdog` | Detects absence of progress beyond a timeout |
| `RequestRejectionReason` | Why a request was refused, in a form a caller can branch on |
| `IElevatorEventSink` | Observability port: what happened, not how it is recorded |
| `ElevatorEvent` and its cases | The closed set of things the system reports |
| `ElevatorRunner` (simulator) | Drives `ProcessRequests()` on a clock |
| `LoggingElevatorEventSink` (simulator) | Turns events into structured log lines |
| `SimulationScenario` (simulator) | A scripted sequence of passengers, so a run is reproducible |

The dependency direction is strictly one-way: the simulator depends on the core, never the
reverse. `Elevator` depends on `IElevatorSchedulingStrategy` rather than on FIFO specifically, so
swapping the algorithm is a composition-root change and touches no existing class.

### 4.1 The elevator state machine

```text
                 request queued
      ┌──────────────────────────────────┐
      │                                  ▼
   ┌──────┐  target above   ┌──────────────┐  arrived   ┌───────────┐
   │ Idle │────────────────►│  MovingUp    │───────────►│ DoorOpen  │
   │      │  target below   │  MovingDown  │            │           │
   └──────┘◄────────────────└──────────────┘            └───────────┘
      ▲            queue empty                                │
      └───────────────────────────────────────────────────────┘
                            door dwell elapsed
```

Every transition is driven by `Step()`, which advances the machine by exactly one tick and is
fully deterministic given a clock reading. Transitions that the machine does not allow — opening
a door mid-travel, moving past the top floor — are rejected as invalid operations rather than
silently ignored.

## 5. Concurrency model

The system is **multi-producer, single-consumer**.

```text
 N caller threads ──► ElevatorController.RequestElevator()
                       │  validate (pure)  →  ConcurrentQueue.Enqueue()   [lock-free, O(1)]
                       ▼
 1 consumer      ──► ProcessRequests()   [serialised by a gate]
                       │  drain queue → Elevator.AddRequest() → Elevator.Step()
                       ▼
                     publish immutable ElevatorSnapshot   [Volatile.Write]
                       ▲
 any thread ──────► GetSnapshot()    [Volatile.Read, wait-free, never a torn state]
```

Three properties follow from this shape:

**Producers never block.** Enqueueing is lock-free, so request-admission latency is bounded by
allocation, not by contention. The 100 ms requirement is met by construction rather than by
tuning.

**The mutable domain is reached by one thread at a time.** `ProcessRequests` takes a gate before
touching the car, so the state machine needs no internal locking and stays readable. The system
is meant to be driven by a single runner, but correctness does not depend on a host honouring
that: request assignment cannot race because the gate admits one thread at a time.

**Reads are consistent and wait-free.** Observers do not read live fields. After each processing
cycle the controller publishes an immutable `ElevatorSnapshot`, and readers take that reference
through `Volatile.Read` — reference assignment is atomic and the volatile read supplies
visibility, so an observer never blocks and never blocks the car.

The alternative — one coarse lock around the whole elevator — was rejected: it makes every
observer contend with the runner and it hides the state machine inside a critical section.

## 6. Time and testability

Nothing in the core calls `DateTime.Now`, `Task.Delay`, or `Thread.Sleep`. All timing flows
through `TimeProvider`, injected by constructor, with durations configured in `ElevatorOptions`:
travel time per floor, door dwell time, and stuck-detection timeout.

The simulator injects `TimeProvider.System`. Tests inject `FakeTimeProvider` and advance the
clock explicitly, which makes the entire suite deterministic and near-instantaneous. A test suite
that sleeps is a test suite that flakes, and flakiness is exactly what a reviewer looks for in a
concurrency exercise.

## 7. Error handling

**Invalid floors.** A passenger asking for floor 42 is expected traffic, not an exceptional
condition, so `RequestElevator` returns an explicit `RequestResult` carrying a rejection reason.
Exceptions are reserved for programming-contract violations such as a null strategy.

**Stuck elevators.** `StuckElevatorWatchdog` defines "stuck" as absence of progress rather than
by cause: a change of floor or of state is progress, and no change for longer than `StuckTimeout`
is a stall, whatever produced it. An idle car with nothing to do is exempt; an idle car with
passengers waiting is not. On detection the car is withdrawn from service — the stall is reported
once, the car is no longer advanced, and new requests are refused with a typed reason a caller can
branch on. Returning it is a deliberate human act, never automatic.

**Concurrent failures.** The processing loop isolates faults at three levels. A request that
cannot be scheduled is reported and the rest of the queue is still drained. A failure while
advancing the car is reported and leaves the system observable rather than tearing down the
host. And a sink that throws is discarded, because observation must never break the thing it
observes.

## 8. Testing strategy

Tests are written before the implementation in every pull request.

**Unit tests** cover state transitions and their guards, FIFO ordering, floor validation, door
dwell behaviour, and watchdog timing. Table-driven `[Theory]` cases carry the combinatorial
surface so the intent stays visible.

**Concurrency tests** live in their own project, because measuring is a different activity from
asserting on behaviour. They drive 3,200 requests from 64 threads, run four consumers against one
car at once, and observe continuously while the system is under load — the direct executable form
of requirements N1-N5.

**Performance tests** measure p99 admission latency in three conditions — quiet, while the car is
being driven, and with 64 callers contending — and fail above 100 ms, proving requirement N6
rather than asserting it in prose. Memory tests pin the per-request cost and check that nothing is
retained once work is served, which is requirement N7 made concrete.

Correctness under concurrency stays in the unit suite, where it is fast and deterministic; only
load and measurement move to the separate project.

**Stack:** xUnit, FluentAssertions, `Microsoft.Extensions.TimeProvider.Testing`.

## 9. Delivery plan

Trunk-based development. Each step branches from `main`, ships as one reviewable pull request,
and is merged before the next begins. Conventional Commits for messages, conventional prefixes
for branches. Architectural decisions are recorded as ADRs in the pull request that makes them.

| # | Branch | Deliverable |
|---|--------|-------------|
| 1 | `chore/project-scaffolding` | Solution, projects, analyzers, `.editorconfig`, `global.json`, CI, PR template |
| 2 | `feat/elevator-domain-model` | Enums, requests, `FloorRange`, `Elevator` state machine + tests |
| 3 | `feat/fifo-scheduling-strategy` | Scheduling abstraction, FIFO implementation, ADR on the trade-off |
| 4 | `feat/elevator-controller` | Thread-safe controller, immutable snapshot, ADR on the concurrency model |
| 5 | `feat/observability-and-events` | Event sink port, domain events, structured logging adapter |
| 6 | `feat/stuck-elevator-detection` | Watchdog, out-of-service policy, clock-driven tests |
| 7 | `feat/console-simulator` | Console host, `ElevatorRunner`, runnable scenario |
| 8 | `test/concurrency-and-performance` | Concurrency suite, latency benchmark, final README |

## 10. Trade-offs and alternatives considered

**FIFO is knowingly suboptimal.** A car travelling from floor 1 to floor 9 will pass a waiting
passenger on floor 5 without stopping, because that request arrived later. This is what the brief
asked for, so it is what is implemented — but the scheduling decision sits behind
`IElevatorSchedulingStrategy`, and the accompanying ADR states what SCAN/LOOK would change and
why it is not being built now.

**A layered Clean Architecture was rejected.** Domain/Application/Infrastructure/Presentation for
one elevator and one algorithm adds four project boundaries that carry no information. SOLID is
applied inside a flat structure instead, where it earns its keep.

**A `Floor` value object was rejected.** The brief specifies `currentFloor: int`. Wrapping it
would diverge from the stated contract to buy validation that `FloorRange` already centralises at
the boundary.

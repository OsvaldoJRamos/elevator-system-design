# Elevator System Design

A thread-safe elevator control system in .NET.

The system models a single elevator serving floors 1 to 10: it accepts pickup requests (floor +
direction) and destination requests from any number of concurrent callers, schedules them with a
FIFO algorithm, and drives the car through its `Idle` / `MovingUp` / `MovingDown` / `DoorOpen`
state machine.

## Design first

The architecture was documented before any code was written:

**[docs/design/elevator-system-design.md](docs/design/elevator-system-design.md)**

That document defines requirements traceability, component boundaries, the concurrency model and
the delivery plan. Every pull request implements one step of it. Architectural decisions are
recorded as ADRs under [`docs/adr/`](docs/adr) as they are made:

| ADR | Decision |
|-----|----------|
| [0001](docs/adr/0001-record-architecture-decisions.md) | Record architecture decisions |
| [0002](docs/adr/0002-serve-requests-first-in-first-out.md) | Serve requests first-in, first-out |
| [0003](docs/adr/0003-serialise-the-car-behind-a-single-consumer-gate.md) | Serialise the car behind a single-consumer gate |
| [0004](docs/adr/0004-derive-events-at-the-boundary.md) | Derive events at the boundary |
| [0005](docs/adr/0005-withdraw-a-stalled-car-and-require-a-human-to-return-it.md) | Withdraw a stalled car and require a human to return it |

## Seeing it run

The simulator plays a scripted scenario against a real clock, with the timings shortened so a run
finishes in seconds. Abridged output:

```text
info: Starting scenario 'Morning rush' with 5 requests, floors 1 to 10.
info: Accepted a pickup on floor 9 going Down.
info: Departing floor 1 going Up.
info: Accepted a pickup on floor 3 going Up.
info: Accepted a destination of floor 6.
warn: Refused a request for floor 42 (FloorOutOfRange): Floor 42 does not exist in this
      building, which serves floors 1 to 10.
info: Accepted a pickup on floor 2 going Up.
info: Doors open on floor 9.
info: Doors closed on floor 9.
info: Departing floor 9 going Down.
info: Doors open on floor 3.
...
info: Scenario complete. The car is parked on floor 2.
```

Two things in that log are worth noticing, because both are deliberate.

The car departs floor 1 for floor 9 and **passes floor 3 without stopping**, even though someone
there is waiting to go up. That is FIFO behaving exactly as the specification asks: the floor 3
request arrived second, so it is served second. A LOOK scheduler would have collected them on the
way — [ADR 0002](docs/adr/0002-serve-requests-first-in-first-out.md) covers why the specified
algorithm was implemented anyway and what switching would involve.

The request for floor 42 is refused rather than throwing. A passenger asking for a floor that does
not exist is ordinary traffic, so it comes back as a result carrying a reason the caller can
branch on.

## Requirements

Every requirement is traceable to the code that satisfies it and the tests that hold it there.

### Functional

| Requirement | Implementation | Covered by |
|-------------|----------------|------------|
| An `Elevator` class represents a single elevator | `Elevator` | `ElevatorTests` |
| An `ElevatorController` manages elevator operations | `ElevatorController` | `ElevatorControllerTests` |
| Handle pickup requests (floor + direction) | `RequestElevator` | `ElevatorControllerTests` |
| Handle destination requests | `RequestDestination` | `ElevatorControllerTests` |
| States `IDLE` / `MOVING_UP` / `MOVING_DOWN` / `DOOR_OPEN` | `ElevatorState` | `ElevatorMovementTests` |
| `moveUp` / `moveDown` / `openDoor` / `closeDoor` | `Elevator` | `ElevatorTransitionGuardTests` |
| Queue management for floor requests | `AddRequest`, `TargetFloors` | `ElevatorTests` |
| Simple FIFO scheduling | `FifoSchedulingStrategy` | `FifoSchedulingStrategyTests`, `ElevatorSchedulingTests` |
| Basic movement simulation | `ElevatorRunner`, `SimulationHostedService` | `ElevatorRunnerTests` |
| Simple logging of elevator actions | `LoggingElevatorEventSink` | `LoggingElevatorEventSinkTests`, `ElevatorObservabilityTests` |

### Non-functional

| Requirement | How it is met | Covered by |
|-------------|---------------|------------|
| Thread-safe operations for concurrent requests | Multi-producer / single-consumer pipeline | `ElevatorControllerThreadSafetyTests`, `ConcurrentLoadTests` |
| Atomic state changes | Immutable snapshot published as one reference write | `ElevatorControllerThreadSafetyTests` |
| No race conditions in request assignment | Consumer serialised by a gate | `ConcurrentLoadTests` |
| Thread-safe collections | `ConcurrentQueue` for ingress | `ConcurrentLoadTests` |
| 100+ concurrent requests handled efficiently | Lock-free admission | `ConcurrentLoadTests` |
| Assignment response time under 100 ms | Admission never waits for the car | `AdmissionLatencyTests` |
| Reasonable memory under load | Small immutable records, nothing retained after service | `MemoryUnderLoadTests` |
| Invalid floor requests handled gracefully | `RequestResult` with a typed reason | `ElevatorControllerTests` |
| Timeouts for stuck elevators | `StuckElevatorWatchdog` + withdrawal from service | `StuckElevatorWatchdogTests`, `ElevatorOutOfServiceTests` |
| Exception handling for concurrent operations | Fault isolation at three levels in the processing loop | `ElevatorObservabilityTests` |

## Measured behaviour

The performance requirements are measured, not asserted. These figures are printed by the test run
itself:

| Measurement | Result | Budget |
|-------------|--------|--------|
| Admission latency, quiet system (p99) | 0.0002 ms | 100 ms |
| Admission latency, while the car is being driven (p99) | 0.0002 ms | 100 ms |
| Admission latency, 64 concurrent callers (p99) | 0.0020 ms | 100 ms |
| Memory per admitted request | 68 bytes | — |
| Requests admitted from 64 threads with none lost | 3,200 | 100+ |
| Snapshots observed under load, all internally consistent | 83,834 | — |

Admission sits orders of magnitude inside the budget because it is lock-free by construction
rather than tuned: a caller validates a request, puts it on a queue, and returns. The tests exist
to fail if that property is ever quietly lost — for instance by someone adding a lock to the
admission path.

## Getting started

Requires the .NET SDK pinned in [`global.json`](global.json).

```bash
dotnet build -c Release      # must produce zero warnings
dotnet test  -c Release      # runs the full suite
dotnet run --project src/ElevatorSystem.Simulator
```

To reproduce the measured figures above:

```bash
dotnet test tests/ElevatorSystem.Core.ConcurrencyTests -c Release --logger "console;verbosity=detailed"
```

## Repository layout

```text
src/ElevatorSystem.Core/                     domain model — depends on nothing outside the BCL
src/ElevatorSystem.Simulator/                console host: composition root, clock, logging
tests/ElevatorSystem.Core.UnitTests/         behaviour, one transition at a time
tests/ElevatorSystem.Core.ConcurrencyTests/  load, latency and memory under contention
tests/ElevatorSystem.Simulator.Tests/        the host and its logging adapter
docs/design/                                 architecture and design document
docs/adr/                                    architecture decision records
```

The domain library has no dependency on any logging, hosting or DI package. Everything the system
is made of is chosen in one place — `Program.cs` in the simulator — so swapping FIFO for another
algorithm, or the console log for a different sink, is a change to that file alone.

## Engineering conventions

Quality gates are enforced by the build rather than by review habit:

- **Warnings are errors.** `TreatWarningsAsErrors` plus .NET analyzers at `latest-recommended`.
- **Code style is enforced at compile time** through `.editorconfig` and
  `EnforceCodeStyleInBuild`; CI additionally runs `dotnet format --verify-no-changes`.
- **Public API in `src/` must be documented** — an undocumented public member fails the build.
- **NuGet versions are managed centrally** in `Directory.Packages.props`, so version drift
  between projects cannot happen.
- **The domain boundary is asserted by a test**, not just by convention — see
  `CoreDependencyTests`.
- **Tests never sleep.** Every duration in the system flows through `TimeProvider`, so the suite
  drives a fake clock and finishes in milliseconds rather than waiting for timeouts it could
  simply skip past.

Commits follow [Conventional Commits](https://www.conventionalcommits.org/); each step of the
delivery plan is a single reviewable pull request against `main`.

## License

[MIT](LICENSE)

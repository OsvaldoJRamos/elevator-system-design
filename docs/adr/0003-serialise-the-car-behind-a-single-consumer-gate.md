# 3. Serialise the car behind a single-consumer gate

**Status:** Accepted · **Date:** 2026-09-08

## Context

The brief requires thread-safe operation, no race conditions in request assignment, more than 100
concurrent requests handled efficiently, and elevator assignment in under 100 ms.

The elevator itself is a state machine over mutable fields: a floor, a state, a queue. Something
has to stop two threads from advancing it at once. The question is what, and where.

The obvious answer — a lock inside `Elevator`, taken by every public member — is the one to
examine first, because it is what most submissions do.

## Decision

Make the system multi-producer, single-consumer, with three distinct rules:

**Producers never block.** `RequestElevator` and `RequestDestination` validate the request and
enqueue it on a `ConcurrentQueue`. There is no lock on this path at all, so admission latency is
bounded by an allocation rather than by contention. The 100 ms requirement is met by construction,
not by tuning.

**One consumer at a time.** `ProcessRequests` takes a gate before touching the car. It is expected
to be driven by a single runner, but the gate means correctness does not depend on a host honouring
that expectation. Inside it, the elevator's mutable state is reached by exactly one thread, which
is why `Elevator` holds no locks and reads as ordinary sequential code.

**Readers take nothing.** After each cycle the controller publishes an immutable
`ElevatorSnapshot` and `GetSnapshot` reads that reference through `Volatile.Read`. Reference
assignment is atomic and the volatile read supplies visibility, so an observer never blocks, never
blocks the car, and can never see a half-updated state.

## Consequences

The three roles are separated, so each can be reasoned about alone: admission is lock-free,
mutation is serialised, observation is wait-free. A reviewer asking "what happens if two threads
do X at once?" gets a different, short answer for each of the three.

`Elevator` stays free of concurrency concerns entirely. Its tests drive it single-threaded and
assert behaviour, not interleavings, which is why the state machine has 60-odd tests that run in
milliseconds.

The cost is that state is published, not live. `GetSnapshot` reports the car as of the last
processing cycle, so an observer polling faster than the runner sees repeats. The pending-request
count is read from the ingress queue at call time rather than from the snapshot, specifically so a
caller who has just submitted a request sees it reflected immediately instead of appearing to have
been dropped.

A second consequence is that nothing happens without a caller. The car makes no progress unless
something calls `ProcessRequests`, which is a deliberate inversion: the core owns no threads, and
the decision of how often to advance the system belongs to the host that starts one.

## Alternatives considered

**A lock inside `Elevator`, held by every public member.** Rejected. It makes every observer
contend with the runner, so reading the floor for a display competes with moving the car. It
spreads the concurrency across every method of the state machine rather than confining it to one
type. And it does not actually solve request assignment: two threads could still interleave
`AddRequest` and `Step` in an order that loses work, because the invariant spans both calls rather
than living inside either.

**An actor: a dedicated background thread owning the car, fed by a `Channel`.** Rejected, but this
was the closest alternative and it is what a larger system should do. It buys the same guarantees
plus back-pressure, at the cost of the core owning a thread and a lifecycle — start, stop, drain,
dispose — that a single-elevator exercise does not need. `ProcessRequests` deliberately leaves that
choice to the host: a console loop, a `BackgroundService`, or a test calling it by hand all work,
and the test suite exercises the third.

**`ImmutableInterlocked` / lock-free compare-and-swap over the whole elevator state.** Rejected as
unjustified cleverness. It would replace a gate that is uncontended in the intended design with
retry loops that are harder to read and no faster here.

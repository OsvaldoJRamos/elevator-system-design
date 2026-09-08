# 4. Derive events at the boundary rather than emitting them from the domain

**Status:** Accepted · **Date:** 2026-09-08

## Context

The brief asks for logging of elevator actions. Something has to turn "the car moved from 2 to 3"
into a record someone can read.

The natural first instinct is to hand `Elevator` an event sink and have it report its own
movements: the car knows exactly what it did, at the moment it did it. That is where the
information is.

## Decision

Do not give the car a sink. `ElevatorController` records the car's floor and state before calling
`Step`, reads them again afterwards, and reports the difference.

`IElevatorEventSink` is the domain's only outbound port. It describes *what happened*, never how
it should be recorded, so no logging framework reaches the core — a constraint asserted by
`CoreDependencyTests` rather than left to discipline. The concrete logging adapter lives in the
simulator, on the far side of that boundary.

## Consequences

`Elevator` stays a pure state machine: inputs, transitions, no side effects. Its sixty-odd tests
assert behaviour by reading properties, with no sink to configure and no output to ignore. Adding
a new event later changes one method on the controller and touches the domain not at all.

Sinks are isolated from the system they observe. A sink that throws is caught and discarded,
because observation must never break the thing it observes and there is by definition nowhere left
to report the failure of the thing that reports failures.

The cost is real and worth stating: derived events are inferred from a before-and-after
comparison, so anything that happens and is undone within a single `Step` would be invisible. That
is acceptable because `Step` performs at most one transition by design — the two facts hold each
other up, and if `Step` ever became multi-transition this decision would need revisiting.

A second cost is that the controller now knows how to interpret state changes, which is a little
more logic in a type that already owns concurrency. It is a small amount, and the alternative
spreads reporting across every method of the state machine.

## Alternatives considered

**`Elevator` publishes its own events.** Rejected. It puts a side effect in every transition of
what is otherwise a pure state machine, and it means every test of the car has to supply a sink or
tolerate one. The gain — perfect fidelity for sub-step events — buys nothing, because there are no
sub-step events.

**C# events (`event EventHandler<...>`) instead of a sink interface.** Rejected. Multicast
delegates make the failure semantics worse: one throwing handler stops the rest from running, and
subscription lifetime becomes something callers have to manage. A single-method interface is
easier to implement, easier to fake in a test, and has exactly one failure mode to reason about.

**Log directly from the core with `Microsoft.Extensions.Logging`.** Rejected. It would put a
third-party dependency in the domain, break the assertion that the core depends on nothing outside
the BCL, and decide the recording format on behalf of every future host.

# 2. Serve requests first-in, first-out

**Status:** Accepted · **Date:** 2026-09-08

## Context

The brief specifies "a simple FIFO (First In, First Out) scheduling algorithm". Taken literally,
that is not how any real elevator behaves, and the difference is not subtle.

Under FIFO the car serves whichever request arrived first, regardless of where it is. A passenger
on floor 1 asks for floor 8; a second passenger on floor 3 presses the call button a moment later.
The car travels from 1 to 8, **passing floor 3 with someone standing there**, discharges its
passenger, and only then travels back down. The second passenger waits for a round trip they
watched the car make.

Real installations use LOOK — a variant of the SCAN family — in which the car continues in its
current direction, stopping at every requested floor along the way, and reverses only when nothing
further remains ahead. In the scenario above LOOK stops at floor 3 on the way up, and the second
passenger waits seconds instead of a minute.

So the algorithm named in the brief is known to be the wrong one for the problem domain. The
question is what to do about that.

## Decision

Implement FIFO exactly as specified, and put the decision behind
`IElevatorSchedulingStrategy` so that replacing it requires a new class and one line in the
composition root — not an edit to the state machine.

The interface receives a `SchedulingContext` carrying the car's floor and state. `FifoSchedulingStrategy`
ignores both. That is not waste: an algorithm denied the car's position could never outperform
FIFO, so a seam that withheld it would be decoration rather than an extension point.

The cost of the algorithm is pinned down by an executable test,
`Step_ServesRequestsInArrivalOrderEvenWhenThatMeansPassingAFloorTwice`, which asserts that the car
passes floor 3 without stopping. If someone later "fixes" the scheduler without meaning to, that
test fails and asks them whether they intended to change the specified behaviour.

## Consequences

The brief is satisfied literally, which matters: a submission that quietly delivered LOOK instead
would be answering a question nobody asked, and its author's judgement about following a
specification would be the thing under review.

The known weakness is contained rather than hidden. `FifoSchedulingStrategy` documents the
trade-off at the point of implementation, and the elevator no longer knows what algorithm it is
running — a property proven by a test that drives the same elevator with a nearest-floor-first
strategy and observes the stop order change.

Switching to LOOK later is a new class implementing the same interface plus a different line where
the elevator is constructed. Nothing in the state machine, the request model, or the tests for
either has to change.

What this does *not* buy is a better elevator today. Under load, FIFO produces longer average
waits and more total travel than LOOK, and no amount of interface hygiene changes that. The
abstraction makes the algorithm replaceable; it does not make it good.

## Alternatives considered

**Implement LOOK instead.** Rejected: it contradicts an explicit requirement. The right move when
a specification looks wrong is to implement it and say so, which is what this record does.

**Implement FIFO with opportunistic stops** — keep arrival order, but open the doors at any queued
floor the car happens to pass. This is tempting because it is nearly free and removes the worst of
the waiting. Rejected for the same reason: it is no longer "simple FIFO", and the brief asked for
simple FIFO.

**Hard-code FIFO with no abstraction.** Rejected, but it was the closest call. One implementation
behind an interface is ordinarily speculation. It earns its place here because the algorithm is
the one part of this system the brief itself frames as a choice, and because the seam is what lets
the trade-off be demonstrated rather than merely asserted.

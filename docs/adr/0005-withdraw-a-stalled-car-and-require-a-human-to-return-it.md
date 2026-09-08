# 5. Withdraw a stalled car from service and require a human to return it

**Status:** Accepted · **Date:** 2026-09-08

## Context

The brief asks for timeouts for stuck elevators. That phrasing settles less than it appears to,
because it names a mechanism without saying what the mechanism is for. Three questions remain:
what counts as stuck, what happens when it is detected, and who decides it is over.

A car can fail to make progress for reasons that look nothing alike from the inside: a motor that
seizes mid-travel, a door sensor that never reports the door closed, a scheduler that declines to
choose. Enumerating causes would mean catching only the ones anticipated in advance, which is
exactly the set that does not matter.

## Decision

**Define stuck by absence of progress, not by cause.** `StuckElevatorWatchdog` records the car's
floor and state on each cycle. A change in either is progress. No change for longer than
`StuckTimeout` is a stall, whatever produced it. The watchdog is deliberately ignorant of why,
which is what lets it catch causes nobody anticipated — a test drives it with a seized motor and
another with a scheduler that refuses to choose, and it does not distinguish them.

**One exception: an idle car with nothing to do is not stuck**, however long it sits there. An
elevator nobody has called is a working elevator. An idle car with passengers waiting, on the
other hand, is broken — a car that will not depart is as useless as one that will not arrive.

**On detection, withdraw the car from service.** Report the stall, stop advancing the car, and
refuse new requests with `RequestRejectionReason.ElevatorOutOfService`. A stall is reported once
rather than on every subsequent cycle: it is an event, not a condition to be re-announced.

**Returning to service is a human act.** `ReturnToService` exists, nothing calls it automatically,
and work admitted before the fault is kept.

## Consequences

A broken car stops pretending to work. Before this, a seized elevator would have absorbed requests
forever and told every passenger their request was accepted. Now the first one to ask after the
fault is told the truth, in a form their code can branch on rather than a string they would have
to parse.

Rejection reasons became a typed enum in this step rather than the free text they were in step 4.
The distinction is now actionable: a floor that does not exist will never exist, whereas a car out
of service will come back, so a caller can retry one and must not retry the other. This is the
second reason for refusal, which is the point at which the enum stops being speculation.

Nothing recovers automatically, and that is the intended cost. Automatic recovery would mean the
system deciding a fault it cannot diagnose has gone away, which in a physical installation is how
a car with a jammed door gets driven anyway. The system is willing to declare itself broken and
wait.

The watchdog is not thread-safe. It does not need to be — it is owned by the controller and only
ever touched inside the processing gate — but that is a constraint a future maintainer has to
respect, and it is documented on the type.

## Alternatives considered

**Time each operation individually: a travel timeout, a door timeout, a dispatch timeout.**
Rejected. It is more precise and strictly worse: three timeouts to configure, three code paths to
maintain, and it only ever catches the three failure modes someone thought of. The
progress-based check caught the "scheduler refuses to choose" case for free, and that case was
not on the list when the watchdog was written.

**Report the stall but keep driving the car.** Rejected. If the car is genuinely stuck, continuing
to accept passengers for it makes the failure worse and hides it behind a queue that grows
forever. Withdrawal is what makes the fault visible to the people it affects.

**Recover automatically after a cool-down.** Rejected as the wrong default. It would convert a
hard failure into an intermittent one, which is harder to diagnose than the original. A host that
genuinely wants a retry policy can build one on top of `ReturnToService`; the core should not
assume it.

**Add an `OutOfService` state to `ElevatorState`.** Rejected. The brief specifies exactly four
states, and out-of-service is a property of the installation rather than of what the car is
physically doing — the car is still idle, or still stopped mid-travel. It is reported on the
snapshot instead, where it does not disturb a state machine the brief defined.

# Elevator System Design

A thread-safe elevator control system in .NET.

The system models a single elevator serving floors 1 to 10: it accepts pickup requests (floor +
direction) and destination requests from any number of concurrent callers, schedules them with a
FIFO algorithm, and drives the car through its `Idle` / `MovingUp` / `MovingDown` / `DoorOpen`
state machine.

## Design first

The architecture is documented before any code is written:

**[docs/design/elevator-system-design.md](docs/design/elevator-system-design.md)**

That document defines the requirements traceability, the component boundaries, the concurrency
model, and the delivery plan. Every pull request in this repository implements one step of it.

Architectural decisions are recorded as ADRs under [`docs/adr/`](docs/adr) as they are made.

## Status

Implementation in progress — see the [pull requests](../../pulls) for the step-by-step delivery.

## License

[MIT](LICENSE)

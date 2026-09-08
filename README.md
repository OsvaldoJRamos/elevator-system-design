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
recorded as ADRs under [`docs/adr/`](docs/adr) as they are made.

## Getting started

Requires the .NET SDK pinned in [`global.json`](global.json).

```bash
dotnet build -c Release      # must produce zero warnings
dotnet test  -c Release      # runs the full suite
dotnet run --project src/ElevatorSystem.Simulator
```

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

## Repository layout

```text
src/ElevatorSystem.Core/           domain model — depends on nothing outside the BCL
src/ElevatorSystem.Simulator/      console host: composition root, clock, logging
tests/ElevatorSystem.Core.UnitTests/
tests/ElevatorSystem.Simulator.Tests/
docs/design/                       architecture and design document
docs/adr/                          architecture decision records
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

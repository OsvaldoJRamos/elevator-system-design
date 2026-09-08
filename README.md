# Elevator System Design

[![CI](https://github.com/OsvaldoJRamos/elevator-system-design/actions/workflows/ci.yml/badge.svg)](https://github.com/OsvaldoJRamos/elevator-system-design/actions/workflows/ci.yml)

A thread-safe elevator control system in .NET, built as a coding challenge.

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

## Repository layout

```text
src/ElevatorSystem.Core/           domain model — depends on nothing outside the BCL
src/ElevatorSystem.Simulator/      console host: composition root, clock, logging
tests/ElevatorSystem.Core.UnitTests/
docs/design/                       architecture and design document
docs/adr/                          architecture decision records
```

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

Commits follow [Conventional Commits](https://www.conventionalcommits.org/); each step of the
delivery plan is a single reviewable pull request against `main`.

## License

[MIT](LICENSE)

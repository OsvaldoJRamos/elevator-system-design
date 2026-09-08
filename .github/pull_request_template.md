## What this pull request does

<!-- One paragraph: the change and why it is needed. -->

## Requirements covered

<!--
Reference the identifiers from docs/design/elevator-system-design.md, e.g. F3, N1.
Say "none" for pure infrastructure changes.
-->

## Design notes

<!-- Anything a reviewer should understand before reading the diff: trade-offs, rejected alternatives. -->

## How it was verified

<!-- Commands run and their outcome. Evidence, not assertions. -->

```
dotnet build -c Release
dotnet test  -c Release
```

## Checklist

- [ ] Behaviour is covered by tests, written before the implementation
- [ ] `dotnet build -c Release` produces no warnings
- [ ] `dotnet test -c Release` is green
- [ ] Public API in `src/` is documented with XML comments
- [ ] Architectural decisions are recorded in `docs/adr/`
- [ ] The design document still matches the code

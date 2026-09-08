# Architecture Decision Records

Each file here captures one decision: the context that forced it, the option chosen, and the
consequences accepted along with it. A decision is recorded in the same pull request that makes
it, so the reasoning stays attached to the change instead of being reconstructed later.

Records are immutable. When a decision is revisited, a new record supersedes the old one and both
are kept — the history of what was believed at the time is the point.

Format: [Michael Nygard's template](https://cognitect.com/blog/2011/11/15/documenting-architecture-decisions).

## Index

| ID | Title | Status |
|----|-------|--------|
| [0001](0001-record-architecture-decisions.md) | Record architecture decisions | Accepted |
| [0002](0002-serve-requests-first-in-first-out.md) | Serve requests first-in, first-out | Accepted |
| [0003](0003-serialise-the-car-behind-a-single-consumer-gate.md) | Serialise the car behind a single-consumer gate | Accepted |
| [0004](0004-derive-events-at-the-boundary.md) | Derive events at the boundary rather than emitting them from the domain | Accepted |

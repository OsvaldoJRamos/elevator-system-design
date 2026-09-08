# 1. Record architecture decisions

**Status:** Accepted · **Date:** 2026-09-08

## Context

This repository is delivered as a sequence of pull requests, each one a step of the plan in
[the design document](../design/elevator-system-design.md). Several of those steps involve
choices that are not visible in the resulting code: why FIFO rather than SCAN, why a
single-consumer pipeline rather than a lock around the elevator, why `TimeProvider` rather than
`Task.Delay`.

Code shows what was built. It does not show what was rejected, and a reviewer who cannot see the
rejected options cannot tell a deliberate decision from an oversight.

## Decision

Record every architectural decision as a numbered file in `docs/adr/`, following Michael Nygard's
template: context, decision, consequences.

A record is written in the same pull request that makes the decision. Records are immutable — a
superseded decision is marked as such and a new record replaces it, rather than being edited in
place.

## Consequences

Reviewers can evaluate the reasoning, not just the result, and the "why" survives past the
conversation in which it was decided.

The cost is one short document per architectural choice. Routine implementation choices do not
get a record; only decisions that constrain future work do.

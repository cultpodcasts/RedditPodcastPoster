# ADR 0005: FileKeyClaims committed rows are permanent

- **Status:** Accepted
- **Date:** 2026-11-28
- **Related:** #1307

## Context

The Cosmos `FileKeyClaims` container holds leases while a podcast, TV show, news organisation or film is being created (`committed: false`, item `ttl` ≈ 1 day). After a successful save, `FileKeyClaimStore.Acknowledge` sets `committed: true` and `ttl: -1`.

Ops saw committed rows accumulating and wondered whether the container was meant to be temp-only storage that should expire or be purged.

## Decision

**Keep committed claims forever** (option 1 on #1307).

1. Committed FileKeyClaims are the durable uniqueness store for file keys (kept out of LookUps on purpose). Growth with the catalogue is **expected**.
2. “Temp” applies **only** to the uncommitted lease window (`UncommittedLifetimeSeconds`). After acknowledge, the marker must not expire.
3. Do **not** TTL/prune committed rows unless uniqueness moves to another store in a deliberate redesign.
4. Explicit deletes remain for compensation paths (e.g. kind-transfer) that release a key — that is not scheduled purge.

## Consequences

- Agents and ops treat a growing `FileKeyClaims` document count as normal; do not “clean up” committed rows without an apply-approved redesign.
- Optional follow-up (out of scope here): Cosmos RU / document-count metrics or alerts for unexpected growth rates — not required to accept this decision.
- Docs: `FileKeyClaim` / `AGENTS.md` / container bicep comments state committed = permanent.

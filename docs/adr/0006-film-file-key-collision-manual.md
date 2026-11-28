# ADR 0006: Film public file-key collisions stay manual

- **Status:** Accepted
- **Date:** 2026-11-28
- **Related:** #1041, PR #1039 (`PublicFileKeyGuard`)

## Context

Public database films are published as `{fileKey}.json`, typically `film-{slug(title)}`. Two live films with the same derived key (remake, alternate cut) collide in one folder. `PublicFileKeyGuard` fails the DatabasePublisher run (exit 1), names both films, and tells the operator to set a different stored `fileKey` in Cosmos. The publisher **never** renames items itself.

#1041 asked whether create/migration should instead assign a deterministic unique key (for example `film-{slug}-{year}`).

## Decision

**Keep the status quo: fail publish on collision; disambiguate with a manual Cosmos `fileKey` edit.**

1. Uniqueness is enforced at publish time by `PublicFileKeyGuard`, not invented at publish time.
2. Stored `fileKey` values are stable once published — a second film with the same title must not force renaming the first film’s public file.
3. Deterministic create-time suffixes (year, cut, id fragment) are **deferred** until product explicitly asks for that allocator behaviour. Remakes are rare enough that operator edit is acceptable.
4. When editing, keep the `film-` prefix (e.g. `film-{slug}-2` as the guard message suggests).

## Consequences

- Operators (and agents) treat a film file-key collision as a Cosmos data fix, not a publisher bug.
- Create/migration paths may still default to `film-{slug(title)}`; collisions remain possible until a future ADR changes allocation.
- Do not add publish-time auto-suffixing — that would change published names when a second film appears.

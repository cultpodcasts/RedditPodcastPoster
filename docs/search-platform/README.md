# Search platform — docs index

**Replace Azure AI Search Free tier with a Cloudflare D1 FTS5 engine**, drop-in behind
the existing api Worker `/search` proxy (`env.apihost` swap).

| Doc | Use when |
|-----|----------|
| **[agent-handoff.md](./agent-handoff.md)** | Ticket body is thin — start here. Session learnings, constraints, how to claim SP-* work |
| **[switchover-plan.md](./switchover-plan.md)** | Gated milestones G0→G5, ticket map, board rules |
| **[d1-query-engine.md](./d1-query-engine.md)** | Implementing / changing query translation, analyzer, response shape, ranking |
| **[sync-and-replication.md](./sync-and-replication.md)** | Drip updates, weekly reset, Durable Objects decision, global read replication |
| **[parity-evidence.md](./parity-evidence.md)** | Measured Azure↔D1 agreement (harness + production log replay) |
| **[known-bugs.md](./known-bugs.md)** | Production bugs found during parity (proxy errors, podcastName latch) |
| Parent epic (history + decisions) | [../search-platform-epic.md](../search-platform-epic.md) |
| 2026-07 alternatives survey | [../search-platform-alternatives.md](../search-platform-alternatives.md) |

## Status (2026-11-16)

- **Prototype:** Api `search-demo/` Worker on remote D1 (WEUR), Azure-shaped `POST …/docs/search`
  (unmerged [Api PR #214](https://github.com/cultpodcasts/Api/pull/214), last touched 8 Nov 2026).
- **Parity baseline (8 Nov 2026):** analyzer 0.01% residual miss; week of real traffic
  (13,070 shapes) → **0 in-corpus misses**. Details in [parity-evidence.md](./parity-evidence.md).
- **Tickets:** epic `SP-E` = RedditPodcastPoster#1312; label `epic:search-platform`; board
  [cultpodcasts/projects/1](https://github.com/users/cultpodcasts/projects/1)
- **Not done:** production sync path, service binding, apihost cutover, Azure retirement,
  title/AKA phrase-first ranking (SP-1.8, required before cutover).

## Decisions (16 November 2026, 10:14 GMT, Europe/London)

Jon:

1. **SP-0.3 ([RPP #1320](https://github.com/cultpodcasts/RedditPodcastPoster/issues/1320), closed):**
   D1 indexes **full descriptions and guests from day one of the soak**. This is option A,
   plus guests pulled forward from G5. There is no temporary strict-content soak index.
   Schema and sync land in G1/G2; G3 soak starts with both already present.
   [SP-5.2 (#1325)](https://github.com/cultpodcasts/RedditPodcastPoster/issues/1325) now
   only drops leftover Azure cap-driven slimming after retirement.
2. **Title / AKA phrase-first ranking is required before cutover.** Unquoted queries that
   are an exact title or an exact `alternativeTitles` AKA must rank that document first.
   Quoted searches keep working as phrases. Acceptance is on
   [SP-1.8, Api #225](https://github.com/cultpodcasts/Api/issues/225); also noted on
   [SP-G4, #1317](https://github.com/cultpodcasts/RedditPodcastPoster/issues/1317).
   This must not slip past G4.

## Post-epic gaps (after the 8 Nov 2026 baseline)

The November demo and parity run predate later Azure-index work. The production engine
and sync path must include these, not only the 8 Nov snapshot:

| Gap | Source | Where it lands |
|-----|--------|----------------|
| Film `alternativeTitles` (searchable; Azure uses `standard.lucene`) | RPP #1431; e.g. "Father's House" → Faderhuset | G1 schema + G2 projection; ranking on SP-1.8 |
| Retrievable identity ids (`imdbId`, `tmdbId`, `tvdbId`, series variants) | RPP #1437 | G1 schema + G2 projection |
| Unquoted exact title / AKA phrase-first ranking | Jon, 16 Nov 2026 | SP-1.8 (required before G4) |
| Guests + full (uncapped) descriptions from soak day one | SP-0.3, 16 Nov 2026 | G1 schema + G2 sync; present at G3 start |
| `FilmSearchPusher` stays until films are indexed on D1 | #1408 / AGENTS.md | Do not retire in G5 until that is true |

## Hard rules (always)

1. **One agent ↔ one `SP-*` ticket.** Do not expand into neighbour IDs without a new ticket.
2. **No production deploys** of `api` / `api-preview` / website Pages / Azure Functions unless
   the user names that exact deploy in the conversation.
3. **Preview/production secrets parity** for any new Worker secret (names in PR
   `## Config / secrets`, never values).
4. **CQRS:** commands acknowledge; clients GET for the read model.
5. *(Historical)* The CatalogueMigrate CLIs were retired in #1369; search rebuild was never
   part of the catalogue migration.

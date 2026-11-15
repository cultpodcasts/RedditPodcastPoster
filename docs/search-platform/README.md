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

## Status (2026-11-08)

- **Prototype:** Api `search-demo/` Worker on remote D1 (WEUR), Azure-shaped `POST …/docs/search`
- **Parity:** analyzer 0.01% residual miss; week of real traffic (13,070 shapes) → **0 in-corpus misses**
- **Tickets:** epic `SP-E` = RedditPodcastPoster#1312; label `epic:search-platform`; board
  [cultpodcasts/projects/1](https://github.com/users/cultpodcasts/projects/1)
- **Not done:** production sync path, service binding, apihost cutover, Azure retirement

## Hard rules (always)

1. **One agent ↔ one `SP-*` ticket.** Do not expand into neighbour IDs without a new ticket.
2. **No production deploys** of `api` / `api-preview` / website Pages / Azure Functions unless
   the user names that exact deploy in the conversation.
3. **Preview/production secrets parity** for any new Worker secret (names in PR
   `## Config / secrets`, never values).
4. **CQRS:** commands acknowledge; clients GET for the read model.
5. *(Historical)* The CatalogueMigrate CLIs were retired in #1369; search rebuild was never
   part of the catalogue migration.

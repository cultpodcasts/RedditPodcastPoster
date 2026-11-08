# Search platform — agent handoff

Read this when an `SP-*` ticket is missing detail. Prefer this folder over reconstructing
chat history. Parent narrative: [../search-platform-epic.md](../search-platform-epic.md).

## How to find and claim work

| Mechanism | Convention |
|-----------|------------|
| Label | `epic:search-platform` (all three repos) |
| Title | `[search-platform] SP-… — …` |
| Search | `is:issue label:epic:search-platform` or `is:issue in:title SP-1.5` |
| Board | [cultpodcasts/projects/1](https://github.com/users/cultpodcasts/projects/1) Todo |
| Stable IDs | `SP-E`, `SP-G0`…`SP-G5`, `SP-{gate}.{n}` — never reuse |

**Independently workable** (ticket header field): an agent can open only that issue + the
files it names and finish it. If `Blocked by: SP-…`, wait until that issue is Done.

Gates are hard: do not start Gate N work until Gate N−1’s gate ticket (`SP-GN-1`) is Done.
See [switchover-plan.md](./switchover-plan.md).

## What already exists (do not rebuild)

| Piece | Where |
|-------|--------|
| Demo search Worker | `cultpodcasts/Api` → `search-demo/` (Azure-shaped `/indexes/…/docs/search`) |
| Query translator | `search-demo/src/query.ts` — simple syntax + en.lucene mirroring |
| OData filter subset | `search-demo/src/index.ts` — eq/ne/and/or, `search.in`, subjects/any, orderby, facets, select |
| Parity harness | `search-demo/harness/` — website OData cases + free-text corpus |
| Remote D1 | `search-demo-db`, region **WEUR**, ~239 MB / 89,245 docs, FTS `porter unicode61` |
| Autosuggest (unchanged by swap) | RPP `SearchSuggestionsIndexBuilder` → R2 → Api `searchSuggestions` route |

Epic PRs (context, not merge requirements for every ticket):

- Api demo/parity: `cultpodcasts/Api` PR #214 (`cursor/search-d1-demo-0b84`)
- This docs epic: RPP PR on `cursor/search-platform-epic-0b84`

## Critical behavioural facts (measured)

1. **Azure simple syntax has no AND/OR/NOT keywords.** They are plain terms then
   stopword-dropped. Verified live: `scientology AND "paulette cooper"` → OR result on
   Azure. Translator must match (not implement boolean keywords).
2. **Diacritics:** Azure `en.lucene` does **not** fold accents (`agora` ≠ `ágora`). D1
   `unicode61` folds by default — D1 is a **superset** here (intentional enhancement;
   switchable with `remove_diacritics 0` if strict parity ever wins).
3. **Free text is a D1 superset** (full descriptions vs Azure ~100-char truncation, plus
   guests/curator terms). Filter/browse families match modulo corpus freshness.
4. **Lone negation** (`-"Davi Vieira"`): Azure searchMode=any → everything-except;
   D1 → 0. Documented intentional divergence (Azure is the footgun).
5. **Traffic mix (week):** 93.7% subject filters, 4.7% shortener `id eq`, **1.5% free text**.
   Filters are the parity-critical path.
6. **Release sort** is id-for-id identical on filtered pages; within same-second ties,
   order differs — add `id` as secondary sort (SP-1.7).
7. **D1 overload** under heavy concurrent `COUNT(*)` surfaced as HTTP 400 until the demo
   Worker returned **503** for runtime failures (retryable). Keep that distinction.

## Cross-repo touch map

| Concern | Repo |
|---------|------|
| Search Worker, D1 schema, admin upsert, service binding, apihost | `cultpodcasts/Api` |
| `/search` proxy error forwarding (`src/search.ts`) | `cultpodcasts/Api` |
| `SearchIndexerService` dual-write, reconcile timer, retire Azure indexer | `cultpodcasts/RedditPodcastPoster` |
| legacyName latch / empty-body 400 heuristic | `cultpodcasts/website` |
| Board add helper | `scripts/add-issue-to-features-board.ps1` (RPP) |

## When the ticket is thin — checklist

1. Open [README.md](./README.md) → this file → the tech doc for your gate (engine / sync / bugs).
2. Confirm **Blocked by** is Done; confirm gate predecessor is Done.
3. Implement only the ticket’s acceptance lines; open a follow-up `SP-*` for discovered scope.
4. PR: include `## Config / secrets` with **names only** if any new secret/binding.
5. Test: Api → harness / translator unit tests; RPP → `dotnet build` + `dotnet test` as
   usual; website → existing component specs for search/latch.
6. Do not deploy production Workers/Pages/Functions unless explicitly asked.

## Open product decision (SP-0.3)

Cutover behaviour for free text:

- **A (recommended):** keep full-description recall (D1 better than Azure) as the default.
- **B:** temporary strict-content index for soak, then flip to full recall.

Record the choice in the epic and on SP-E before G4 messaging.

# Production bugs found during search parity work

Tickets: **SP-1.5** (Api), **SP-1.6** (website), **SP-4.3** (stale clients).

## 1. api Worker `/search` proxy discards upstream errors — SP-1.5

**File:** `cultpodcasts/Api` → `src/search.ts`

On any non-200 from Azure (or future D1 engine):

```ts
return c.json(response.body, 400);
```

Problems:

- `response.body` is a `ReadableStream` → client gets **`400 {}`**
- Azure’s message (`Could not find a property named 'podcastName'`) is lost
- Every upstream status (429, 503, 403, real 400) collapses to **400**

**Fix:** `await response.text()` (or `.json()`), forward Azure’s error envelope; preserve
status class (pass 4xx through; map 5xx/429 to retryable statuses). Keep `searchLog`
`searchStatus` behaviour.

**Acceptance:** missing-field filter returns 400 with named field in body; upstream 429
is not returned as opaque 400.

## 2. Website empty-body 400 → legacy `podcastName` latch — SP-1.6

**File:** `cultpodcasts/website` → `cultpodcasts/src/app/playable-search-hit.ts`
(`isUnknownSearchFieldError`, `nextLegacyNameLatch`; call-sites in series-hub,
podcast-api, search-api, subject-api, playable-detail-shell).

Because of bug 1, the website treats **any** HTTP 400 with an empty body as “unknown
search field” and retries with `podcastName` (legacy name from pre-`seriesName` index).
Any transient opaque 400 can latch legacy mode and show “Something went wrong.”

**Fix (after SP-1.5):** detect missing field only via
`Could not find a property named '…'`; **delete** the empty-body-400 branch.

History: website PR #504 (2026-09) introduced the latch for index rename
`podcastName` → `seriesName`. Current code always tries `seriesName` first.

## 3. Stale clients still send `podcastName` — SP-4.3

**Evidence (Workers Logs, exact day window):** exactly two requests in the week, both
Azure **400**:

- `(podcastName eq 'Associated Press')`
- `(contentKind eq 'NewsReport') and (podcastName eq 'Associated Press')`

Same-day `seriesName` variants for that page were all **200** — so these were not the
live latch firing; almost certainly a **stale tab / cached bundle** from before the
rename.

**Mitigations:**

- D1 FIELD_MAP already maps `podcastName` → `seriesName` (stale clients work after swap)
- Optional: api proxy rewrite `podcastName`→`seriesName` in filter/facets while still on
  Azure
- Self-diminishing population (~2 req/week)

## Related non-bugs (do not “fix” without a ticket)

| Observation | Disposition |
|-------------|-------------|
| D1 free-text count > Azure | Full descriptions — intentional |
| `-"Davi Vieira"` Azure 88k vs D1 0 | Documented divergence |
| Related-rail count +1/+2 on Azure | Corpus freshness vs snapshot |
| Top-10 rank order differs | bm25 weights (SP-1.8), not a production incident |

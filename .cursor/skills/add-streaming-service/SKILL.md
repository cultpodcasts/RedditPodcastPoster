---
name: add-streaming-service
description: >-
  Add a new streaming-provider plugin (matcher + page extractor + catalog key)
  across RedditPodcastPoster, Api Worker contract, and website catalog/matcher.
  Use when the user asks to add a streaming service, new scraper plugin, support
  a new *.tv / SVOD URL, or make streaming plugins faster via scaffold.
---

# Add streaming service

Ship a Channel4-shaped non-podcast scraper so `GET /submit/lookup` returns
`kind: streaming` + `service`, and prepare/submit can extract meta.

**Primary repo:** RedditPodcastPoster. Api owns the copied wire-key contract; website
mirrors catalog + matcher. RPP authority is the `StreamingService` enum
(`[JsonPropertyName]` wire key + `[StreamingServiceInfo]` display/icon/hosts).

## When to use

- User names a new host (e.g. france.tv) or “add streaming service X”
- User asks for an `add-streaming-service` skill / scaffold
- Extending submit beyond Spotify / Apple / YouTube

## Inputs (ask once)

| Field | Example |
|-------|---------|
| `key` | `franceTv` (camelCase; `[JsonPropertyName]` on `StreamingService`) |
| `displayName` | `France TV` |
| `hosts` | `france.tv` |
| `icon` | `france-tv` (website icon slug) |
| Sample series URL | brand / hub page |
| Sample episode URL | watch / `.html` episode |
| Film URL (optional) | expect `ShowName` null |
| `htmlFetchMode` | default `directHttp`; only `browserRendering` if bare HTTP extract fails |

## Safety

- No Api / website `wrangler deploy` / `npm run deploy`
- No PR merge unless user explicitly asks
- No Cosmos episode writes without explicit `--apply`
- PR `## Config / secrets` only if Worker `browserRenderingServices` (or other secrets) change — document **names** for preview **and** top-level `api`
- Azure Functions deploy only when user asks (`deploy functions & clis` or named app)

## Steps

### 1. Scaffold (RPP)

From RedditPodcastPoster root:

```powershell
pwsh ./scripts/scaffold-streaming-service.ps1 `
  -Key <key> `
  -DisplayName "<displayName>" `
  -Hosts <host1>,<host2> `
  -Icon <icon-slug>
```

Creates `Class-Libraries/RedditPodcastPoster.<Pascal>/` + `.Tests` from Channel4 shape
(host-only matcher stub, OG extractor, DI, registration). Then hand-edit path grammar
and ShowName / film rules against live HTML.

### 2. Wire catalog + DI (RPP)

- `Models/Podcasts/StreamingService.cs` — enum member with `[JsonPropertyName("key")]` + `[StreamingServiceInfo(display, icon, wideImage, hosts…)]` in search-encode order
- `KnownStreamingServices.cs` — `*StreamingService.Registration` matching enum order
- `PodcastServices/.../ServiceCollectionExtensions.cs` — `.Add*Services()` in `AddNonPodcastScrapers`
- `RedditPodcastPoster.slnx` — library + test projects (scaffold prints paths)
- `StreamingCatalog` project reference if scaffold did not add it

Do **not** add a parallel string-const class or a second adapter enum.
`SearchIndexCosmosSql` is parameterized from catalog keys — no manual SQL edit in repo when
`KnownStreamingServices` is updated. **Do** refresh the live Azure Search datasource after
ship (step 8) — generated SQL in git ≠ query stored on `cultpodcasts-ds`.

### 3. Hand-tune matcher + extractor

Template: `RedditPodcastPoster.Channel4`.

- Matcher: host + path (series + episode); reject marketing/shallow paths
- Extractor: HTTP GET + `OpenGraphPageMetaDataExtractor`; `Publisher` = platform brand
- `ShowName` only for true series; films/one-offs → null
- Never treat publisher as series (`NonPodcastShowNameResolver`)

### 4. Tests (RPP)

- Plugin `BusinessRules/*` (unit-tests.mdc: DisplayName, Arrange/Act/Assert, no brand names in filler)
- `StreamingScraperCanonicalCases.cs` — live cases (CI skips via `SKIP_LIVE_STREAMING_SCRAPER_TESTS=1`)
- Browse pages only if homepage harvest is useful

```powershell
pwsh ./scripts/assert-unit-test-guardrails.ps1 -GitChanged
dotnet test Class-Libraries/RedditPodcastPoster.<Pascal>.Tests -c Release
dotnet test Class-Libraries/RedditPodcastPoster.UrlSubmission.Tests -c Release --filter DisplayName~Streaming
```

### 5. Api contract (source of truth for TS/JSON copies)

Edit `Api/tests/fixtures/streaming-submit-contract.ts` **and** `.json`:

- `streamingServiceKeys` (must equal `StreamingServiceWire.AllKeys`)
- `streamingSpecimenUrls`
- membership + orchestration case ids (fixture generators usually expand)

Bump Api `package.json` + `package-lock.json` patch.

Copy JSON to `RedditPodcastPoster/docs/contracts/streaming-submit-contract.json`.

```powershell
# RPP
pwsh ./scripts/assert-streaming-submit-contract-copy.ps1
```

### 6. Website parity

- Copy TS contract → `cultpodcasts/src/app/streaming-submit-contract.ts`
- `service-catalog.ts` — catalog row + `resolveServiceKey` host
- `podcast-url-matcher.ts` — series + episode regex
- `tools/icon-sources/paths.json` — icon path data; regenerate icons if repo script requires
- Specs for catalog / matcher
- Bump `cultpodcasts/package.json` + lockfile

```powershell
# website git root
pwsh ./scripts/assert-streaming-submit-contract-copy.ps1
# from cultpodcasts/
npm run test:all   # before push when shipping client code
```

### 7. Browser Rendering (only if needed)

If prepare extract fails on direct HTTP (SPA shell / soft wall):

- Add key to Worker secret `browserRenderingServices` CSV (preview **and** top-level `api`)
- Document in PR `## Config / secrets`
- Keep contract `defaultBrowserRenderingServices` as ops hint only

### 8. Search index (HARD — do after RPP with the new key is on the machine you run CLIs from)

New streaming keys do **not** add Azure Search fields. They only appear inside the existing
`svc` string (and `image` coalesce). **Never recreate / teardown the live index** for a new
plugin.

Catalog C# already generates pull-path Cosmos SQL from `StreamingServiceCatalog.SearchEncodedKeys`
(`SearchIndexCosmosSql`). Azure’s **stored** datasource query does **not** auto-update when you
merge code — episodes submitted before that SQL bump can land in search with empty `svc` /
`image` even when Cosmos `services.{key}` is populated.

**Mandatory ops after the RPP change that adds the enum/catalog key is available to CLIs**
(merged to `main` + published tools, or `dotnet run` from that commit):

```powershell
# 1) Upsert cultpodcasts-ds Cosmos projection (includes new key in svc + image coalesce).
#    Does NOT recreate the index. Does NOT rewrite existing documents by itself.
CreateSearchIndex `
  --update-existing `
  --index cultpodcasts `
  --datasource cultpodcasts-ds

# 2) Rewrite search docs for podcasts already submitted on this service (push path from Cosmos).
#    Prefer targeted reindex over --reset-indexer (free-tier pull catch-up is multi-day).
Index --reindex-search -n "<PodcastName>"
# or: Index --reindex-search --podcast-id <guid>

# 2b) If Cosmos has services.{key}.url but release/duration were empty at first submit
#     (extractor improved later), re-scrape and overwrite via episode id — no URL needed:
SubmitUrl -r -e <episode-guid>
```

Spot-check search REST: `filter=id eq '<episodeId>'` → non-empty `svc` starting with
`{key}:` (or containing `|{key}:`) and sensible `release` / `duration` / `image` when Cosmos has them.

Optional bulk (avoid unless corpus-wide): add `--indexer cultpodcasts-indexer --reset-indexer`
to step 1, then run the indexer over multiple days.

Document in the RPP PR that search datasource refresh is required post-merge (or do it in the
same delivery session). Details: [`docs/episode-services.md`](../../docs/episode-services.md)
§ Ops: after datasource SQL update.

### 9. Done when

1. Matcher accepts real series + episode URLs; rejects lookalike hosts
2. Extractor returns title + publisher; ShowName rules correct
3. Lookup → `kind: streaming`, `service: <key>`; podcastName never = platform name
4. DI registered; catalog + website + Api contract aligned
5. Unit tests green; live Theories added; assert-contract scripts clean
6. Live `cultpodcasts-ds` projection updated (`CreateSearchIndex --update-existing`); sample
   streaming episodes show non-empty `svc` after push reindex (not “index recreate”)

## Template paths (Channel4)

| Role | Path |
|------|------|
| Enum | `Class-Libraries/RedditPodcastPoster.Models/Podcasts/StreamingService.cs` |
| Registration | `Class-Libraries/RedditPodcastPoster.Channel4/Channel4StreamingService.cs` |
| Matcher | `.../Matching/Channel4UrlMatcher.cs` |
| Extractor | `.../Extractors/Channel4PageMetaDataExtractor.cs` |
| DI | `.../Extensions/ServiceCollectionExtensions.cs` |

## Related

- `docs/streaming-submit-orchestration.md` (Api)
- `docs/handoff-streaming-provider-scrapers.md` (show-name + live gates; procedure = this skill)
- Website `docs/submit-url-flows.md`

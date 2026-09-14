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
`SearchIndexCosmosSql` is parameterized from catalog keys — no manual SQL edit when
`KnownStreamingServices` is updated.

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

### 8. Done when

1. Matcher accepts real series + episode URLs; rejects lookalike hosts
2. Extractor returns title + publisher; ShowName rules correct
3. Lookup → `kind: streaming`, `service: <key>`; podcastName never = platform name
4. DI registered; catalog + website + Api contract aligned
5. Unit tests green; live Theories added; assert-contract scripts clean

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

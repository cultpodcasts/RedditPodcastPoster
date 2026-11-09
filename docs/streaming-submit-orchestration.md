# Streaming submit orchestration (api-infra / RPP)

Azure Functions side of streaming catalogue URL ingest. **Canonical wire contract** is published by the Cloudflare Api Worker repo:

- Source: `Api/tests/fixtures/streaming-submit-contract.json`
- Local copy: [`contracts/streaming-submit-contract.json`](./contracts/streaming-submit-contract.json)
- Process rules: `Api/docs/streaming-submit-orchestration.md`

Assert:

```powershell
# from RedditPodcastPoster git root
pwsh ./scripts/assert-streaming-submit-contract-copy.ps1
```

## RPP obligations

1. **`StreamingServiceCatalog.SearchEncodedKeys`** must equal contract `streamingServiceKeys` and `StreamingServiceWire.SubmitEligibleKeys` (AllKeys minus submit-retired; Hulu is retired — enforced by `StreamingSubmitContractRules`). Image coalesce may still include retired enum keys for historical URLs.
2. **Membership** (`GET api/SubmitUrl`):
   - Returns `service` (ServiceKeys) for streaming URLs.
   - Does **not** scrape HTML. Unknown streaming returns `{ known: false, kind: streaming, service }` with `podcastName` null.
   - Prepare owns HTML fetch / show-name extract. Contract flag `membershipDoesNotScrape: true` is live.
3. **Prepare** (`POST api/SubmitUrl/prepare`) fetches HTML via adapter `ExtractMetaData(url)` and returns meta + `service`.
4. **Extract** (`POST api/SubmitUrl/extract`) accepts trusted HTML or JSON (`ExtractMetaData(url, html)`) — Worker Browser Rendering / regional scrape Worker path, and BitChute video-API JSON prefetched by the Worker.

Worker prepare chooses **how** (`htmlFetchMode` / `scrapeProfiles.mode`) and **where** (`scrapeProfiles.region`: `default` on Api, or Phase 1 `us` via `streaming-scrape-us`). See Api `docs/streaming-submit-orchestration.md` § Browser Rendering allowlist + scrape profiles.

Any service listed in contract `scrapeProfiles` or `defaultBrowserRenderingServices` **must** register `extractFromHtml` on `CatalogKeyedNonPodcastServiceAdapter` (see Peacock / Tubi / Itvx). Without that delegate, Azure extract throws `NotSupportedException` (`HTML extract is not registered for service '…'`).
5. **Submit** accepts trusted `prefetchedMeta` from the Worker when present — no second page fetch. Contract v2: `prefetchedMeta` must carry `madeAsFilm` (`prefetchedMetaFields`; Worker KV `stream-meta:v2:`), and prepare/extract always write it, so a cached streaming film is still classified as a Film rather than a podcast episode. When `submitContentTypes:Enabled` is true, submit classifies the scraped page and may persist a Film, TvShow episode, or NewsReport instead of a Podcast and Episode. The flag defaults to false, and while it is false submit still writes a Podcast and Episode. Membership may include optional `contentKind` and `parentName` only while the flag is on. Film has no `parentName`. A BBC News page (`bbc.co.uk/news/…`, `bbc.com/news/…` on any BBC subdomain, the bare `/news` section page, or the legacy `news.bbc.co.uk` host, but not `/news/av-embeds/…` media embeds, which stay iPlayer catalogue URLs; see `BbcNewsSubmitRejection.Applies`) is **rejected** (#1211 G5): submit answers 400 `{ contentKind: "NewsReport", rejected: true, message }` and membership answers `rejected: true` with the same `message`. The rejection wins over a curator's parent target or podcast name (*"BBC News pages can't be submitted. Submit the BBC iPlayer episode, or the video from the BBC News YouTube channel."*). Nothing is categorised or stored; BBC news comes in only via BBC iPlayer or the BBC News YouTube channel. YouTube news-station submit stays an explicit signal (`YouTubeNewsStation`); there is no channel-name allowlist on this path.
6. Podcast-service platforms remain API-based — not in this contract.

## Tests

| Artifact | Role |
|----------|------|
| `UrlSubmission.Tests/BusinessRules/Contracts/StreamingSubmitContractRules.cs` | JSON ↔ ServiceKeys + rule flags + case-id completeness |

## Related

- Website: `website/cultpodcasts/docs/streaming-submit-orchestration.md`
- Existing URL membership rules: `UrlSubmission.Tests/BusinessRules/UrlSubmission/UrlMembershipLookupRules.cs`

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

1. **`ServiceCatalog.SearchEncodedKeys`** must equal contract `streamingServiceKeys` (enforced by `StreamingSubmitContractRules`).
2. **Membership** (`GET api/SubmitUrl`):
   - Returns `service` (ServiceKeys) for streaming URLs.
   - Does **not** scrape HTML. Unknown streaming returns `{ known: false, kind: streaming, service }` with `podcastName` null.
   - Prepare owns HTML fetch / show-name extract. Contract flag `membershipDoesNotScrape: true` is live.
3. **Prepare** (`POST api/SubmitUrl/prepare`) fetches HTML via adapter `ExtractMetaData(url)` and returns meta + `service`.
4. **Extract** (`POST api/SubmitUrl/extract`) accepts trusted HTML (`ExtractMetaData(url, html)`) — Worker Browser Rendering path.
5. **Submit** accepts trusted `prefetchedMeta` from the Worker when present — no second page fetch.
6. Podcast-service platforms remain API-based — not in this contract.

## Tests

| Artifact | Role |
|----------|------|
| `UrlSubmission.Tests/BusinessRules/Contracts/StreamingSubmitContractRules.cs` | JSON ↔ ServiceKeys + rule flags + case-id completeness |

## Related

- Website: `website/cultpodcasts/docs/streaming-submit-orchestration.md`
- Existing URL membership rules: `UrlSubmission.Tests/BusinessRules/UrlSubmission/UrlMembershipLookupRules.cs`

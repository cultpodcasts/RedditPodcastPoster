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
2. **Membership** (`GET api/SubmitUrl`) will return `service` for streaming URLs (ServiceKeys). Lookup does **not** scrape HTML once prepare exists.
3. **Prepare extract** accepts trusted HTML / returns `NonPodcastServiceItemMetaData` (implementation PR).
4. **Submit** accepts trusted `prefetchedMeta` from the Worker when present — no second page fetch.
5. Podcast-service platforms remain API-based — not in this contract.

## Tests

| Artifact | Role |
|----------|------|
| `UrlSubmission.Tests/BusinessRules/Contracts/StreamingSubmitContractRules.cs` | JSON ↔ ServiceKeys + rule flags + case-id completeness |

## Related

- Website: `website/cultpodcasts/docs/streaming-submit-orchestration.md`
- Existing URL membership rules: `UrlSubmission.Tests/BusinessRules/UrlSubmission/UrlMembershipLookupRules.cs`

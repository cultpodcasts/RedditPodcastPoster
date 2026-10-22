# Api edge-cache purge after R2 publishes

Some Api Worker routes (cultpodcasts/Api) keep the response in `caches.default`, keyed by the **full request URL**. After a new object is written to R2 they keep serving the old copy until it expires (`max-age`), unless the URL is purged in Cloudflare.

Nothing in this repo calls the Cloudflare API, and there is **no token or app setting** for this. Instead:

- **`PublishR2`** prints a clearly bannered `ACTION:` block at the end of a run, with the exact Cloudflare CLI command to purge the URLs for the targets **actually published in that run**, plus a one-line verify step.
- **Indexer `SearchSuggestionsPublish`** (weekly timer) logs the same command at **Information** after a successful publish (search App Insights traces for `Api cache purge command`). It does not purge.

The URL list lives in one place: `Class-Libraries/RedditPodcastPoster.ContentPublisher/ApiCache/ApiCacheRoutes.cs` (tests: `ApiCachePurgeCommandBuilderRules`, `R2PublishProcessorRules`, `SearchSuggestionsPublishTriggerRules`).

## Which routes are cached

| R2 key | Api route | Api source | Cache | Purged URLs |
| --- | --- | --- | --- | --- |
| `search-suggestions` | `GET /search-suggestions` | `src/searchSuggestions.ts` | `caches.default`, `max-age=3600`, header `X-Search-Suggestions-Cache: HIT/MISS` | `https://api.cultpodcasts.com/search-suggestions`, `https://api-preview.cultpodcasts.com/search-suggestions` |
| `homepage` | `GET /homepage` | `src/homepage.ts` | `caches.default`, `max-age=300`, header `X-Homepage-Cache: HIT/MISS` | `https://api.cultpodcasts.com/homepage`, `https://api-preview.cultpodcasts.com/homepage` |
| `languages`, `people`, `subjects`, `flairs`, `discovery-info`, `homepage-ssr` | `/languages`, `/people`, `/subjects`, `/flairs`, `/discovery-info`, `/homepage-ssr` | | streamed from R2 per request, not edge-cached | none |

The website requests these with no query string (`new URL('/search-suggestions', environment.api)`, `new URL('/homepage', environment.api)`), so the purge URLs have none. Production (`api.cultpodcasts.com`) and preview (`api-preview.cultpodcasts.com`) both read the same `content` R2 bucket, so both are purged. Both hosts are in the `cultpodcasts.com` zone.

If an Api route starts caching another R2 key, add it to `ApiCachedContent` / `ApiCacheRoutes` and mark it published in `R2PublishProcessor`.

## Running the command

Run it in PowerShell on a machine with the Cloudflare CLI (`cf`, a Volta shim `cf.cmd` on Jon's MSI) that is signed in with access to the `cultpodcasts.com` zone (needs **Zone > Cache Purge**). Example printed for `PublishR2 search-suggestions`:

```powershell
cf cache purge -z cultpodcasts.com --body '{\"files\":[\"https://api.cultpodcasts.com/search-suggestions\",\"https://api-preview.cultpodcasts.com/search-suggestions\"]}' --force
```

The JSON quotes are backslash-escaped on purpose: without that, the `.cmd` shim strips them. URLs are batched at most 30 per command (Cloudflare allows 100 per single-file purge request; today there are at most 4).

Verify (printed with the command):

```powershell
curl.exe -s -D - -o NUL https://api.cultpodcasts.com/search-suggestions | Select-String 'X-Search-Suggestions-Cache'
```

Expect `MISS` on the first request after the purge; `HIT` means the old copy is still cached.

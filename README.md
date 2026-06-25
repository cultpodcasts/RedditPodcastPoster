# Cult Podcasts (Reddit Podcast Poster)

A .NET solution for [cultpodcasts.com](https://cultpodcasts.com): discover podcasts, index episodes from multiple providers, enrich metadata, publish to social channels, and expose a public API.

Primary data is stored in **Azure Cosmos DB (v2)** with detached `Podcasts` and `Episodes` containers. Episode search uses **Azure AI Search**.

Licensed under the MIT license.

## Repository layout

| Path | Purpose |
|------|---------|
| `Cloud/` | Azure Functions apps — **Api**, **Discovery**, **Indexer** |
| `Class-Libraries/` | Shared domain, persistence, podcast-service integrations, posting |
| `Console-Apps/` | Local CLI tools for indexing, enrichment, and ops |
| `Infrastructure/` | Bicep templates for Azure resources and function app settings |
| `docs/` | Cost analysis and operational runbooks |

## Azure Functions (production)

| App | Role |
|-----|------|
| `api-infra` | Public HTTP API, URL submission, search indexing triggers |
| `discover-infra` | Scheduled podcast discovery (Listen Notes, Spotify, YouTube, Taddy) |
| `indexer-infra` | Scheduled indexing, categorisation, Reddit/Twitter/Bluesky posting |

Pushes to `main` build and deploy these via [`.github/workflows/deploy.yml`](.github/workflows/deploy.yml). Production app settings are defined in [`Infrastructure/functions.bicep`](Infrastructure/functions.bicep) and applied during provisioning.

**Local code-only deploy** (does not change app settings):

```powershell
.\scripts\deploy-api.ps1
.\scripts\deploy-discover.ps1
.\scripts\deploy-indexer.ps1
```

These call `scripts/deploy-function-local.ps1` internally with the correct `-FunctionName`.

Defaults: resource group `AutomatedInfra`, apps `api-infra` / `discover-infra` / `indexer-infra`, storage `cultpodcastsstg`.

Requires `az login` and appropriate blob upload permissions.

When GitHub Actions is inactive, see [docs/interim-deployment.md](docs/interim-deployment.md) for the full script map and local configuration sources.

## Configuration

### Console apps (local)

Console apps share one user-secrets store (`UserSecretsId` on each `.csproj`). In Development, secrets load automatically via `AddSecrets()`.

Set a value:

```powershell
dotnet user-secrets set "cosmosdbv2:Endpoint" "https://....documents.azure.com:443/" --project Console-Apps/Index/Index.csproj
```

Console apps also accept environment variables prefixed with `RedditPodcastPoster_` (e.g. `RedditPodcastPoster_youtubeChannel__PreferUploadsPlaylist=true`).

### Azure Functions (production)

Function apps use **application settings** with `__` as the section separator (e.g. `cosmosdbv2__Endpoint`, `indexer__ReleasedDaysAgo`). See `functions.bicep` for the canonical list.

### Azure Functions (local)

Cloud projects (`Cloud/Indexer`, `Cloud/Discovery`, `Cloud/Api`) share the same `UserSecretsId` as console apps. In Development, configuration loads from **dotnet user-secrets** — not from `local.settings.json` Values for secrets.

**Do not put API keys in `local.settings.json`.** Use that file only for non-secret host settings (storage emulator, timer disables, indexer tuning). YouTube keys: [`docs/youtube-keys.md`](docs/youtube-keys.md).

```powershell
dotnet user-secrets set "youtube:Applications:13:ApiKey" "YOUR_KEY_PLACEHOLDER" --project Cloud/Indexer/Indexer.csproj
```

To convert a user-secrets JSON file to app-setting names:

```powershell
dotnet run --project Console-Apps/SecretsToFunctionSettings -- path-to-secrets.json
```

### Common settings (local user-secrets)

Use colon notation in user-secrets; Azure uses `__` instead of `:`.

```json
{
  "cosmosdbv2:Endpoint": "https://xxxx.documents.azure.com:443/",
  "cosmosdbv2:AuthKeyOrResourceToken": "xxxx",
  "cosmosdbv2:DatabaseId": "cultpodcasts-db",
  "cosmosdbv2:PodcastsContainer": "Podcasts",
  "cosmosdbv2:EpisodesContainer": "Episodes",
  "cosmosdbv2:SubjectsContainer": "Subjects",
  "cosmosdbv2:ActivitiesContainer": "Activity",
  "cosmosdbv2:DiscoveryContainer": "Discovery",
  "cosmosdbv2:LookUpsContainer": "LookUps",
  "cosmosdbv2:PushSubscriptionsContainer": "PushSubscriptions",
  "cosmosdbv2:UseGateway": false,

  "spotify:ClientId": "xxxx",
  "spotify:ClientSecret": "xxxx",

  "reddit:AppId": "xxxx",
  "reddit:AppSecret": "xxxx",
  "reddit:RefreshToken": "xxxx",

  "youtubeChannel:PreferUploadsPlaylist": true,

  "searchIndex:Url": "https://xxxx.search.windows.net",
  "searchIndex:Key": "xxxx",
  "searchIndex:IndexName": "cultpodcasts",
  "searchIndex:IndexerName": "cultpodcasts-indexer",

  "subreddit:SubredditName": "cultpodcasts",
  "subreddit:SubredditTitleMaxLength": 300,

  "postingCriteria:minimumDuration": "0:09:00",
  "postingCriteria:TweetDays": 2,
  "postingCriteria:RedditDays": 2,
  "postingCriteria:BlueSkyDays": 2,
  "postingCriteria:CategoriserDays": 2,

  "delayedYouTubePublication:EvaluationThreshold": "6:00:00",

  "cloudflare:AccountId": "xxxx",
  "cloudflare:R2AccessKey": "xxxx",
  "cloudflare:R2SecretKey": "xxxx",

  "twitter:ConsumerKey": "xxxx",
  "twitter:ConsumerSecret": "xxxx",
  "twitter:AccessToken": "xxxx",
  "twitter:AccessTokenSecret": "xxxx",

  "bluesky:Identifier": "xxxx",
  "bluesky:Password": "xxxx"
}
```

Additional production-only settings (Auth0, YouTube, Listen Notes, Taddy, push notification keys, etc.) are in `Infrastructure/functions.bicep`. Production secrets are read from Key Vault at **deploy time** (`functions.bicepparam`) and written as **literal** app-setting values — the running app never calls Key Vault. For YouTube key layout, local user-secrets, and interim manual apply steps, see [docs/youtube-keys.md](docs/youtube-keys.md).

### YouTube channel episode retrieval

When a podcast has only a `YouTubeChannelId`, episode lookup can use **Search.List** or the channel **uploads playlist**. Some channels reject search with `accountDelegationForbidden`; the uploads playlist is the reliable fallback.

| Setting | Where | Effect |
|---------|-------|--------|
| `youtubeChannel:PreferUploadsPlaylist` | User secrets (local CLI) | Skip search; use uploads playlist for all channel-only podcasts |
| `youtubeChannel__PreferUploadsPlaylist` | Azure app settings (indexer + api) | Same, in production |

Per-podcast `YouTubeChannelSearchForbidden` is also persisted when search fails at runtime.

### Reddit refresh token

Generate a Reddit refresh token using [Reddit.NET](https://github.com/sirkris/Reddit.NET) — see [this walkthrough](https://www.youtube.com/watch?v=xlWhLyVgN2s).

## Useful console apps

| App | Typical use |
|-----|-------------|
| `Index` | Index episodes for one or more podcasts |
| `Discover` | Run discovery locally |
| `Poster` / `Tweet` | Post episodes to Reddit / Twitter |
| `EnrichExistingEpisodesFromPodcastServices` | Backfill Spotify/Apple/YouTube URLs |
| `EnrichYouTubeOnlyPodcasts` | Enrich YouTube-only channel podcasts |
| `EpisodeDriftDetector` | Compare stored vs provider metadata |
| `SubmitUrl` | Submit a URL via the same path as the API |

Run from the app directory: `dotnet run --project Console-Apps/Index -- [args]`

### Published CLI tools (PATH)

For **local dev convenience**, build console apps into a single folder of standalone executables on your PATH:

```powershell
.\scripts\publish-console-apps.ps1
```

Output defaults to `artifacts\tools\` (gitignored). Add that folder to your PATH, then run tools by name (e.g. `Discover`, `Index`, `Poster`):

```powershell
$tools = (Resolve-Path 'artifacts\tools').Path
[Environment]::SetEnvironmentVariable('Path', "$env:Path;$tools", 'User')
```

| Publish profile | Apps | Why |
|-----------------|------|-----|
| **Self-contained** (default) | Most console apps | Uses reflection-based DI, `CommandLineParser`, Cosmos DB, and reflection JSON — not compatible with Native AOT without broad rewrites. |
| **Native AOT** | `SecretsToFunctionSettings` | Small utility with source-generated JSON and no reflection-based CLI parsing (`CommandLineParser` is incompatible with Native AOT). |

`LaunchSettingsToAppSettings` and other JSON utilities stay self-contained because they use reflection JSON (`JsonNode` navigation) and `CommandLineParser`.

Published tools use the same shared user-secrets store as `dotnet run` (same `UserSecretsId`). Per-app `appsettings.json` files are not copied into `artifacts\tools` (they would overwrite each other); run `dotnet run` from the app directory when you need bundled defaults such as `postingCriteria`.

## Libraries

### Podcast and metadata providers

- [Google.Apis.YouTube.v3](https://developers.google.com/youtube/v3) — YouTube Data API
- [SpotifyAPI.Web](https://github.com/JohnnyCrazy/SpotifyAPI-NET) — Spotify Web API
- [iTunesSearch](https://github.com/danesparza/iTunesSearch) — Apple Podcasts / iTunes lookup
- [PodcastAPI](https://www.nuget.org/packages/PodcastAPI) — Listen Notes discovery and search
- [GraphQL.Client](https://github.com/graphql-dotnet/graphql-client) — Taddy GraphQL API
- [HtmlAgilityPack](https://html-agility-pack.net/) — BBC Sounds and Internet Archive scraping

### Social and publishing

- [Reddit.NET](https://github.com/sirkris/Reddit.NET) — Reddit API (vendored under `Third-Party/`)
- [idunno.Bluesky](https://github.com/idunno/bluesky-sharp) / [X.Bluesky](https://github.com/BlueskySocial/XBluesky) — Bluesky posting
- [OAuth.Net](https://www.nuget.org/packages/OAuth.Net) — Twitter/X OAuth 1.0a
- [WebPush](https://github.com/web-push-libs/web-push-csharp) — browser push notifications

### Azure and hosting

- [Microsoft.Azure.Functions.Worker](https://learn.microsoft.com/azure/azure-functions/dotnet-isolated-process-guide) — isolated .NET Functions (Api, Discovery, Indexer)
- [Microsoft.DurableTask](https://learn.microsoft.com/azure/azure-functions/durable/durable-functions-overview) — orchestrations and timers
- [Microsoft.Azure.Cosmos](https://learn.microsoft.com/azure/cosmos-db/) — document persistence
- [Azure.Search.Documents](https://learn.microsoft.com/azure/search/) — episode search index
- [Azure.AI.TextAnalytics](https://learn.microsoft.com/azure/ai-services/language-service/) — text classification
- [OpenTelemetry](https://opentelemetry.io/) + [Azure Monitor exporter](https://learn.microsoft.com/azure/azure-monitor/app/opentelemetry-enable) — production telemetry

### Storage and CDN

- [AWSSDK.S3](https://github.com/aws/aws-sdk-net/) — Cloudflare R2 (S3-compatible) content storage

### Application framework

- [Microsoft.Extensions.Hosting](https://learn.microsoft.com/dotnet/core/extensions/generic-host) — DI, configuration, logging (console apps and functions)
- [CommandLineParser](https://github.com/commandlineparser/commandline) — CLI argument parsing

### Utilities

- [FuzzySharp](https://github.com/JakeBayer/FuzzySharp) — fuzzy title matching (YouTube episode resolution)
- [Newtonsoft.Json](https://www.newtonsoft.com/json) — Cosmos DB serialization

### Testing

- [xUnit](https://xunit.net/), [FluentAssertions](https://fluentassertions.com/), [Moq.AutoMock](https://github.com/mmoore99/auto-moq), [AutoFixture](https://github.com/AutoFixture/AutoFixture)
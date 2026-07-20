# Console apps

Local CLI tools for indexing, enrichment, discovery ops, search maintenance, and config helpers. They share one user-secrets store (`UserSecretsId` on each `.csproj`) and accept `RedditPodcastPoster_`-prefixed environment variables. See the root [README](../README.md#configuration) for secrets setup.

## How to run

```powershell
# From repo root
dotnet run --project Console-Apps/<AppName> -- [args]

# After publishing to PATH (see below)
<AppName> [args]
```

Published executable name matches the project name (e.g. `Index`, `PublishR2`, `MigrateConfig`).

```powershell
.\scripts\publish-console-apps.ps1
```

Defaults to `artifacts\tools\` (gitignored). `ThrowawayConsole` is excluded from publish. Most apps are self-contained; `MigrateConfig` publishes as Native AOT.

For `--help` on CommandLineParser apps, pass `-- --help` after `dotnet run` (or run the published exe with `--help`).

---

## Core pipeline

### Index

**Purpose:** Index episodes for one or more podcasts (local equivalent of indexer work).

**Run:** `dotnet run --project Console-Apps/Index --` · PATH: `Index`

| Option | Description |
|--------|-------------|
| `-p, --podcast-id` | Podcast GUID |
| `-n, --podcast-name` | Podcast name |
| `-u, --use-single-podcast-name-flow` | Single matching podcast name flow |
| `-r, --released-since` | Index episodes released within this many days (default `2`) |
| `-y, --skip-expensive-youtube-queries` | Skip expensive YouTube queries |
| `-d, --skip-podcast-discovery` | Skip podcast discovery (default `true`) |
| `-s, --skip-expensive-spotify-queries` | Skip expensive Spotify queries |
| `-t, --skip-youtube-url-resolving` | Skip YouTube URL resolution |
| `-f, --skip-spotify-url-resolving` | Skip Spotify URL resolution |
| `-i, --skip-spotify-indexing` | Skip Spotify indexing |
| `-x, --no-index` | Do not reindex search index |
| `-o, --force-index` | Force index-all-episodes |

### Discover

**Purpose:** Run podcast discovery locally (Listen Notes, Spotify, YouTube, Taddy), or pull unprocessed remote discovery results.

**Run:** `dotnet run --project Console-Apps/Discover --` · PATH: `Discover`

| Option | Description |
|--------|-------------|
| `-r, --number-of-hours` | Search window as timespan (`6:10:00`) or whole hours (`7`) — mutually exclusive with `-t` |
| `-t, --time-since` | Discover items released since this time — mutually exclusive with `-r` |
| `-l, --include-listen-notes` | Search Listen Notes (default `true`) |
| `-d, --include-taddy` | Search Taddy (default `true`) |
| `-s, --exclude-spotify` | Exclude Spotify (default `false`) |
| `-y, --include-youtube` | Search YouTube (default `true`) |
| `-e, --enrich-listennotes-from-spotify` | Enrich Listen Notes from Spotify (default `true`) |
| `-a, --enrich-spotify-from-apple` | Enrich Spotify from Apple (default `true`) |
| `-o, --taddy-offset` | Extra Taddy lookback for indexing delay (default `02:00:00`) |
| `-u, --use-remote` | Load/display unprocessed Cosmos discovery from cloud Discover, mark processed; skips live search |

### Poster

**Purpose:** Post recent episodes to Reddit / Twitter / Bluesky (and related publish steps).

**Run:** `dotnet run --project Console-Apps/Poster --` · PATH: `Poster`

| Option / value | Description |
|----------------|-------------|
| `[released-within-days]` | Positional; days window (default `2`) |
| `-p, --podcastid` | Podcast GUID |
| `-e, --episodeid` | Episode GUID |
| `-n, --name` | Podcast name (partial match) |
| `-y, --youtube-primary-post-service` | Prefer YouTube link when available |
| `-g, --ignore-apple-grace-period` | Ignore Apple URL grace period |
| `-t, --skip-tweet` | Skip Tweet |
| `-b, --skip-bluesky` | Skip Bluesky |
| `-w, --skip-publish` | Skip publish |
| `-r, --skip-reddit` | Skip Reddit |
| `-f, --flip-when-ignored` | Flip ignored to false and post |

### SubmitUrl

**Purpose:** Submit a URL (or file of URLs) via the same path as the API.

**Run:** `dotnet run --project Console-Apps/SubmitUrl --` · PATH: `SubmitUrl`

| Option / value | Description |
|----------------|-------------|
| `<url or file>` | Required positional |
| `-f, --submit-urls-in-file` | Treat positional as a file of URLs |
| `-p, --podcastid` | Podcast to add episode to |
| `-y, --skip-youtube-url-enrichment` | Skip YouTube URL resolving |
| `-a, --acknowledge-expensive-queries` | Allow expensive queries |
| `-m, --match-other-services` | Match other services |
| `-d, --dry-run` | Do not commit to database |
| `-i, --no-index` | Do not reindex search index |
| `-l, --is-internet-archive-playlist` | URL is an Internet Archive playlist |
| `-c, --create-podcast` | Create new podcast |

---

## Folded apps (triage consolidations)

### PublishR2

**Purpose:** Publish static content to Cloudflare R2 and subject flairs to Reddit. Replaces former `R2Publisher` / `FlairPublisher`.

**Run:** `dotnet run --project Console-Apps/PublishR2 --` · PATH: `PublishR2`

**Modes** (at most one token; default `languages`):

| Mode | Aliases | Effect |
|------|---------|--------|
| `languages` (default) | `--languages`, `-l` | Publish languages list to R2 |
| `people` | `--people`, `-p` | Publish People register to R2 |
| `flairs` | `--flairs`, `-f`, `flair` | Publish subject flairs to Reddit |
| `all` | `--all`, `-a` | Languages, then people, then flairs |

```text
PublishR2
PublishR2 people
PublishR2 --flairs
PublishR2 all
```

### MigrateConfig

**Purpose:** Convert local config JSON to Azure function app-setting JSON. Native AOT. Replaces `SecretsToFunctionSettings` and `LaunchSettingsToAppSettings`.

**Run:** `dotnet run --project Console-Apps/MigrateConfig --` · PATH: `MigrateConfig`

| Mode | Aliases | Usage |
|------|---------|-------|
| `secrets` | `s` | `MigrateConfig secrets <secrets-json-path>` |
| `launch-settings` | `launchsettings`, `ls` | `MigrateConfig launch-settings <launch-settings-path> <profile-name>` |

```text
MigrateConfig secrets path-to-secrets.json
MigrateConfig launch-settings Cloud/Indexer/Properties/launchSettings.json Indexer
```

### RemoveEpisodes

**Purpose:** Mark matching search episodes as removed, or restore from a prior remove log. `restore` folds former `UnremoveEpisodes`.

**Run:** `dotnet run --project Console-Apps/RemoveEpisodes --` · PATH: `RemoveEpisodes`

**Verbs:**

#### `remove` (default)

| Option / value | Description |
|----------------|-------------|
| `<query>` | Required search query |
| `[throttle]` | Max episodes to remove (default `5`) |
| `-n, --not-whole-term` | Do not treat query as a quoted term |
| `-r, --non-dry-run` | Persist changes (default is dry-run) |

#### `restore`

| Option / value | Description |
|----------------|-------------|
| `<filename>` | Log file listing episodes to restore |

```text
RemoveEpisodes "some query" 10
RemoveEpisodes remove "some query" --non-dry-run
RemoveEpisodes restore removed-episodes-log.txt
```

---

## Enrichment and podcast intake

### EnrichExistingEpisodesFromPodcastServices

**Purpose:** Backfill Spotify / Apple / YouTube URLs on existing episodes.

**Run:** `dotnet run --project Console-Apps/EnrichExistingEpisodesFromPodcastServices --` · PATH: `EnrichExistingEpisodesFromPodcastServices`

| Option | Description |
|--------|-------------|
| `-r, --released-since` | Enrich episodes released within this many days |
| `-y, --skip-youtube-url-enrichment` | Skip YouTube URL resolving |
| `-p, --podcast-id` | Podcast GUID |
| `-n, --podcast-name` | Podcast name |
| `-a, --acknowledge-expensive-queries` | Allow expensive queries |

### EnrichYouTubeOnlyPodcasts

**Purpose:** Enrich YouTube-only channel podcasts (playlist ingest).

**Run:** `dotnet run --project Console-Apps/EnrichYouTubeOnlyPodcasts --` · PATH: `EnrichYouTubeOnlyPodcasts`

| Option | Description |
|--------|-------------|
| `-p, --podcast-id` | Podcast GUID |
| `-i, --youtube-playlist-id` | YouTube playlist id |
| `-n, --podcast-name` | Podcast name |
| `-r, --released-since` | Only ingest items released within these days |
| `-a, --acknowledge-expensive-query` | Acknowledge expensive playlist query |
| `-s, --include-shorts` | Include Short videos |

### EnrichPodcastWithImages

**Purpose:** Enrich podcast or subject images.

**Run:** `dotnet run --project Console-Apps/EnrichPodcastWithImages --` · PATH: `EnrichPodcastWithImages`

| Option | Description |
|--------|-------------|
| `-n, --podcast` | Podcast name / partial match (selector group) |
| `-s, --subject` | Subject to enrich with images (selector group) |

### AddAudioPodcast

**Purpose:** Add a podcast from Spotify or Apple id.

**Run:** `dotnet run --project Console-Apps/AddAudioPodcast --` · PATH: `AddAudioPodcast`

| Option / value | Description |
|----------------|-------------|
| `<podcast-id>` | Required Spotify or Apple id |
| `[episode-title-regex]` | Optional title regex for occasional-cult-series |
| `-a, --apple-podcast-authority` | Use Apple for release authority |
| `-m, --spotify-market` | Spotify market to search |

### AddYouTubeChannelAsPodcast

**Purpose:** Add a YouTube channel as a podcast.

**Run:** `dotnet run --project Console-Apps/AddYouTubeChannelAsPodcast --` · PATH: `AddYouTubeChannelAsPodcast`

| Value | Description |
|-------|-------------|
| `<channel-name>` | YouTube channel name |
| `<most-recent-upload-name>` | Title of the channel’s most recent upload |

### CategorisePodcastEpisodes

**Purpose:** Categorise / subject-tag podcast episodes.

**Run:** `dotnet run --project Console-Apps/CategorisePodcastEpisodes --` · PATH: `CategorisePodcastEpisodes`

| Option | Description |
|--------|-------------|
| `-p, --podcast-ids` | Comma-separated podcast GUIDs |
| `-n, --podcast-partial-match` | Podcast name / partial match |
| `-a, --recent` | Categorise recently indexed episodes |
| `-c, --Commit` | Commit changes |
| `-r, --Reset-Subject` | Reset subjects |

### EpisodeLanguageBackfill

**Purpose:** Backfill episode language fields (dry-run unless `--apply`).

**Run:** `dotnet run --project Console-Apps/EpisodeLanguageBackfill --` · PATH: `EpisodeLanguageBackfill`

| Option | Description |
|--------|-------------|
| `-a, --apply` | Apply changes; without this flag, report only |

### FindDuplicateEpisodes

**Purpose:** Find (and optionally fix) duplicate episodes.

**Run:** `dotnet run --project Console-Apps/FindDuplicateEpisodes --` · PATH: `FindDuplicateEpisodes`

| Option | Description |
|--------|-------------|
| `-n, --not-dry-run` | Execute deletes/updates (default dry-run) |
| `--delete-no-diff` | Delete pure duplicates with no meaningful field diffs (backs up under `dedupe-episodes/`) |
| `-v, --verify-deduplication` | Verify outcomes from backup files against Cosmos |

---

## Search index

### CreateSearchIndex

**Purpose:** Create / tear down Azure AI Search index, data source, and indexer; optionally run the indexer.

**Run:** `dotnet run --project Console-Apps/CreateSearchIndex --` · PATH: `CreateSearchIndex`

| Option | Description |
|--------|-------------|
| `-i, --index` | Index name |
| `-t, --teardown-index` | Tear down index |
| `-d, --datasource` | Data-source name |
| `-x, --indexer` | Indexer name |
| `-r, --run-indexer` | Run the indexer |
| `-m, --run-indexer-max-attempts` | Max rerun attempts on timeout (default `10`) |
| `-p, --run-indexer-poll-seconds` | Poll interval seconds (default `10`) |
| `-b, --not-break-on-duplicates` | Do not break on duplicates (default `true`) |
| `-w, --run-indexer-max-wait-seconds` | Max wait per run before retryable stall (default `30`) |

### DeleteSearchDocument

**Purpose:** Delete a document (or all episodes for a podcast) from the search index.

**Run:** `dotnet run --project Console-Apps/DeleteSearchDocument --` · PATH: `DeleteSearchDocument`

| Option / value | Description |
|----------------|-------------|
| `<document-id>` | Required GUID (episode document, or podcast id with `-p`) |
| `-p, --podcast` | Delete all episodes for the given podcast id |

### AddSubjectToSearchMatches

**Purpose:** Add a subject to episodes matching a search query.

**Run:** `dotnet run --project Console-Apps/AddSubjectToSearchMatches --` · PATH: `AddSubjectToSearchMatches`

| Option / value | Description |
|----------------|-------------|
| `<query>` | Required search query |
| `[throttle]` | Max episodes to update (default `5`) |
| `-s, --add-subject-when-not-subject-match` | Add subject for any search hit (bypass subject match) |
| `-n, --not-whole-term` | Do not treat query as a quoted term |
| `-d, --dry-run` | Do not persist |

---

## Discovery scoring and training

### DiscoveryScoreBackfill

**Purpose:** Score discovery documents (all unprocessed, or specific ids).

**Run:** `dotnet run --project Console-Apps/DiscoveryScoreBackfill --` · PATH: `DiscoveryScoreBackfill`

| Option | Description |
|--------|-------------|
| `-a, --all-unprocessed` | Score all unprocessed discovery documents |
| `-d, --document-ids` | Comma-separated discovery document GUIDs |
| `--dry-run` | Score/analyze without saving to Cosmos |
| `--evidence-path` | Path for markdown evidence report |

### DiscoveryTrainingExport

**Purpose:** Export discovery training CSV from a CosmosDbDownloader export (or analyze existing CSVs).

**Run:** `dotnet run --project Console-Apps/DiscoveryTrainingExport --` · PATH: `DiscoveryTrainingExport`

| Option | Description |
|--------|-------------|
| `-e, --export-path` | Root folder of a CosmosDbDownloader export |
| `-o, --output-path` | CSV output folder (default `<export-path>/analysis`) |
| `-a, --analyze-only` | Skip export; analyze existing CSVs |
| `--analysis-path` | Folder with `discovery-results.csv` (analyze-only override) |

### DiscoveryTrainingTrain

**Purpose:** Train discovery scoring model from `discovery-results.csv`.

**Run:** `dotnet run --project Console-Apps/DiscoveryTrainingTrain --` · PATH: `DiscoveryTrainingTrain`

| Option | Description |
|--------|-------------|
| `-c, --csv-path` | Path to `discovery-results.csv` (required) |
| `-o, --output-path` | Model bundle output directory (required) |
| `-m, --onnx-model-directory` | MiniLM ONNX + vocab dir (default `<output-path>/onnx`) |
| `-s, --show-rates-path` | Optional `show-accept-rates.csv` |
| `--max-rows` | Limit rows for a quick training run |
| `--threshold` | Auto-hide probability threshold in manifest (default `0.05`) |
| `--skip-download` | Skip ONNX download if files exist |

---

## Cosmos and public data

### CosmosDbDownloader

**Purpose:** Download Cosmos containers (podcasts, episodes, subjects, discovery, push subscriptions, known/elimination terms) to local JSON files. No CLI flags — runs the full download.

**Run:** `dotnet run --project Console-Apps/CosmosDbDownloader` · PATH: `CosmosDbDownloader`

### CosmosDbUploader

**Purpose:** Upload local JSON entity files back into Cosmos. No CLI flags — runs the full upload.

**Run:** `dotnet run --project Console-Apps/CosmosDbUploader` · PATH: `CosmosDbUploader`

### CultPodcasts.DatabasePublisher

**Purpose:** Publish a public-facing podcast/episode JSON dump from Cosmos. No CLI flags.

**Run:** `dotnet run --project Console-Apps/CultPodcasts.DatabasePublisher` · PATH: `CultPodcasts.DatabasePublisher`

---

## Ops utilities

### KVWriter

**Purpose:** Create or read shortener (KV) records for episodes.

**Run:** `dotnet run --project Console-Apps/KVWriter --` · PATH: `KVWriter`

| Option | Description |
|--------|-------------|
| `-i, --items` / `-s, --skip` | Batch: take / skip counts |
| `-e, --episode-guid` / `-d, --dry-run` | Single episode shortener create |
| `-r, --read` | Read by short-guid |

### ThrowawayConsole

**Purpose:** Ad-hoc scratch tool (currently probes a YouTube video from an Internet Archive-style URL argument). **Excluded from** `publish-console-apps.ps1`.

**Run:** `dotnet run --project Console-Apps/ThrowawayConsole -- <internet-archive-url>`

---

## Related

- Root README — [Useful console apps](../README.md#useful-console-apps) and [Published CLI tools](../README.md#published-cli-tools-path)
- Publish script: [`scripts/publish-console-apps.ps1`](../scripts/publish-console-apps.ps1)

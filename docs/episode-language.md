# Episode language semantics (HARD)

Authoritative product rules for `Episode.Language` / Cosmos `lang` / search `Lang`.
Implementation entry point: [`EpisodeLanguageResolution`](../Class-Libraries/RedditPodcastPoster.Models/Episodes/EpisodeLanguageResolution.cs).

**Any read-time `episode.Language ?? podcast.Language` (or equivalent ternary) is a corruption of language handling.** It compromises English subject search, language ignored-subjects, title-casing, and curator “set episode to English on a non-English show” behaviour. Business-rule tests under `BusinessRules/**` encode these conventions; failing them means search/enrichment integrity is broken.

---

## Storage

| Intent | Cosmos `Episode.lang` | Notes |
|--------|----------------------|--------|
| English | **`null`** | UI English → POST `lang: ""` → `NormaliseEpisodeLanguage` → null. Do **not** persist `"en"` under current product rules. |
| Non-English | Explicit ISO code (`fil`, `es`, …) | |
| Podcast default | `Podcast.Language` | Applied to **unset** episodes at **write/inherit** time only (`InheritLanguageFromPodcastIfUnset`), not at every read. |

Denormalised `podcastLanguage` on the episode may still be `"fil"` while `lang` is null (English override). Never use `podcastLanguage` as a substitute for `lang` at read time for English/search decisions.

### UI / API path (English)

1. Episode picker: key `unset`, label `English`
2. Change body: `{ "lang": "" }`
3. Worker proxies unchanged
4. `EpisodeChangeApplier.NormaliseEpisodeLanguage`: empty / `en` / `en-*` → **null**

---

## Read-time resolution (HARD)

```csharp
// CORRECT — episode present: null means English
var language = EpisodeLanguageResolution.ForRead(podcast, episode);
// equivalent: episode is not null ? episode.Language : podcast.Language

// CORRECT — known episode document
var language = EpisodeLanguageResolution.ForEpisode(episode); // == episode.Language
```

```csharp
// FORBIDDEN — treats curated English (null) as the podcast's non-English language
var language = episode.Language ?? podcast.Language;
var language = !string.IsNullOrWhiteSpace(episode.Language)
    ? episode.Language
    : podcast.Language;
```

| Situation | Use |
|-----------|-----|
| Episode document present (enrichment, finders, search map, Bluesky, DTO) | `Episode.Language` only (`null` = English) |
| No episode (podcast-only / criteria-only path) | `Podcast.Language` |

### Why coalesce is wrong

On a Filipino podcast, a curator sets one episode to English → Cosmos `lang` is **null**. English subject search includes it via `(lang eq null or lang eq 'en')`. If enrichment or indexing coalesces to `podcast.Language` (`fil`), that episode is treated as Filipino for ignores/title-casing while search still treats it as English — **integrity split**.

---

## Search

- Push mapper [`ToEpisodeSearchRecord`](../Class-Libraries/RedditPodcastPoster.EntitySearchIndexer/Extensions/PodcastEpisodeExtensions.cs): `Lang` from episode only.
- Cosmos SQL indexer: `e.lang as lang` only (not `e.lang ?? e.podcastLanguage`).
- Subject UI default English filter: `(lang eq null or lang eq 'en')` — practically null; `'en'` is legacy/defensive (product does not store `"en"` today).
- English episodes of non-English podcasts **are** in the English subject bucket when `lang` is null.

---

## Write-time inheritance (allowed)

`InheritLanguageFromPodcastIfUnset` / podcast update propagation / `EpisodeLanguageBackfill` may copy `Podcast.Language` onto episodes whose `Language` is null.

**Known risk:** curated English is also stored as null, so a later podcast-language re-propagation can re-stamp `"fil"` onto English episodes. That is a separate write-path concern; it does **not** justify read-time coalesce.

---

## Call sites that must stay aligned

| Area | Rule |
|------|------|
| `SubjectEnrichmentOptionsFactory` | `EpisodeLanguageResolution.ForRead` |
| Spotify/Apple `Create(Podcast, Episode)` | `EpisodeLanguageResolution.ForEpisode` |
| Search `ToEpisodeSearchRecord` | `EpisodeLanguageResolution.ForEpisode` |
| Bluesky | null/blank → `"en"`, else episode lang (not podcast) |
| Episode DTO / title sanitiser | episode lang only |

---

## Tests

Business-rule tests that encode this contract (failure = language/search integrity corruption):

- `Episodes.Tests` / Models: `EpisodeLanguageResolution` anti-coalesce
- `Subjects.Tests` / Enrichment: null episode lang on non-English podcast → English (no language ignores from podcast lang)
- `Indexer.Tests`: `ToEpisodeSearchRecord` leaves `Lang` null despite podcast default
- Spotify/Apple factory rules: `Create(Podcast, Episode)` passes null episode lang through

See also website `subject-language-filter.ts` and `language-options.util.ts`.

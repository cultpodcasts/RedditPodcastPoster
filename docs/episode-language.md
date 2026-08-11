# Episode language semantics (HARD)

Authoritative product rules for `Episode.Language` / Cosmos `lang` / search `Lang`.
Implementation: [`EpisodeLanguageResolution`](../Class-Libraries/RedditPodcastPoster.Models/Episodes/EpisodeLanguageResolution.cs).

Business-rule tests under `BusinessRules/**` encode these conventions. Failing them means search / enrichment / podcast-default integrity is broken.

---

## Storage

| Intent | Cosmos `Episode.lang` | Notes |
|--------|----------------------|--------|
| English | **`null`** | UI English → POST `lang: ""` → `NormaliseEpisodeLanguage` → null. Do **not** persist `"en"` under current product rules. |
| Non-English | Explicit ISO code (`fil`, `es`, …) | |
| Podcast default | `Podcast.Language` | English default is also null. |

Denormalised `podcastLanguage` on the episode may still be `"fil"` while `lang` is null (English override). Never use `podcastLanguage` as a substitute for `lang` at read time.

### UI / API path (English)

1. Episode picker: key `unset`, label `English`
2. Change body: `{ "lang": "" }`
3. Worker proxies unchanged
4. `NormaliseEpisodeLanguage`: empty / `en` / `en-*` → **null**

---

## Read-time resolution (HARD)

```csharp
// CORRECT — episode present: null means English
var language = EpisodeLanguageResolution.ForRead(podcast, episode);

// FORBIDDEN
var language = episode.Language ?? podcast.Language;
```

| Situation | Use |
|-----------|-----|
| Episode document present | `Episode.Language` only (`null` = English) |
| No episode (podcast-only path) | `Podcast.Language` |

---

## Podcast API default-language change (HARD)

When `POST`/`PUT` podcast update includes `language`, after save the API propagates to episodes via `ApplyPodcastDefaultLanguageChange` — **not** `InheritLanguageFromPodcastIfUnset`.

**Rule:** update only episodes that still **follow the previous podcast default**. Null is English, not “unset”.

| Previous podcast default | Episode follows default if… | On change to new default |
|--------------------------|-----------------------------|---------------------------|
| English (`null`) | `episode.lang` is English (`null`) | → new default (null if new is English) |
| `"fil"` | `episode.lang == "fil"` | → new default |
| `"fil"` | `episode.lang` is **null** (English override) | **unchanged** |
| any | `episode.lang` is some other code | **unchanged** |

```csharp
// CORRECT
episode.ApplyPodcastDefaultLanguageChange(previousPodcastLanguage, newPodcastLanguage);

// FORBIDDEN on podcast language change — treats English (null) as unset
episode.SetPodcastProperties(podcast, inheritLanguageIfUnset: true);
```

Capture `previousPodcastLanguage` **before** applying the podcast change request.

### New episode create / merge (different path)

`InheritLanguageFromPodcastIfUnset` remains valid for **brand-new** episodes (create/URL submit): a fresh episode starts with null before inherit, then receives the current show default. That is not a podcast-default *change* and must not be confused with API propagation.

---

## Search

- Push mapper / Cosmos SQL: episode `lang` only (never `lang ?? podcastLanguage`).
- Subject English filter: `(lang eq null or lang eq 'en')` — practically null.
- English episodes of non-English podcasts are in the English subject bucket when `lang` is null.

---

## Call sites

| Area | Rule |
|------|------|
| `SubjectEnrichmentOptionsFactory` | `ForRead` |
| Spotify/Apple `Create(Podcast, Episode)` | `ForEpisode` |
| Search `ToEpisodeSearchRecord` | `ForEpisode` |
| `PodcastUpdateService` language prop | `ApplyPodcastDefaultLanguageChange(previous, new)` |
| New episode create | `InheritLanguageFromPodcastIfUnset` OK |
| Bluesky / DTO / title sanitiser | episode lang only |

---

## Tests (integrity)

- `EpisodeLanguageResolutionRules` — read anti-coalesce; `FollowsPodcastDefault` / default-change matrix
- `EpisodePodcastLanguagePropagationRules` — episode method + previous-default matching
- `PodcastUpdateServiceLanguageInheritanceTests` — API: fil→es moves fil episodes, leaves English null; null→fil moves English-default episodes; clear to English moves previous-default to null
- Enrichment / search / finder rules — null episode lang ≠ podcast lang at read time

See also website `subject-language-filter.ts` and `language-options.util.ts`.

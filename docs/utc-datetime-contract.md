# UTC date-time contract (RPP → Api Worker → website)

Parent ticket: [#1212](https://github.com/cultpodcasts/RedditPodcastPoster/issues/1212).

## Goal

Every **instant** field on the Azure RPP Functions API and the Cloudflare Worker is UTC ISO-8601 with a **`Z` suffix**, serialised and compared consistently. Unspecified-kind `DateTime` values mean UTC (`UtcDateTimes.ToUtc`) — never host-local.

## Exceptions (by design)

| Field / shape | Why |
|---------------|-----|
| Discovery schedule `slotStartUk` | UK-local wall clock for the next slot (offset form, not `Z`) |
| Catalogue `release` | May be year-only (`yyyy`), date-only (`yyyy-MM-dd`), or a full instant (`…Z`) — precision is intentional |

## Date-only releases (Jon’s rule)

A date-only release is **midnight UTC on that calendar date** (`…T00:00:00Z`). It must never shift to the previous or next day via a time-zone conversion (`CatalogueRelease.FromDateTimeUtc` and API `FormatRelease`).

## Hop map

| Hop | Mechanism | Key paths |
|-----|-----------|-----------|
| RPP HTTP JSON | `AddUtcJsonDateTimes()` converters | [`Cloud/Api/Extensions/UtcJsonServiceCollectionExtensions.cs`](../Cloud/Api/Extensions/UtcJsonServiceCollectionExtensions.cs), `UtcDateTimeConverter` / `UtcDateTimeOffsetConverter` |
| RPP Cosmos | Same converters on persistence options | `JsonSerializerOptionsProvider` |
| RPP readers | `UtcDateTimes.ToUtc` / `ToUtcOffset` | [`Class-Libraries/RedditPodcastPoster.Models/Serialization/UtcDateTimes.cs`](../Class-Libraries/RedditPodcastPoster.Models/Serialization/UtcDateTimes.cs) |
| Api Worker OpenAPI | `isoDateTime` requires trailing `Z`; `isoDateTimeLocalOffset` for `slotStartUk` | Sibling [cultpodcasts/Api](https://github.com/cultpodcasts/Api) `src/openapiSchemas.ts` |
| Api Worker comparisons | `toISOString()` / instant parse (hero CAS) | Sibling [cultpodcasts/Api](https://github.com/cultpodcasts/Api) `src/heroCurationLogic.ts` ([#200](https://github.com/cultpodcasts/Api/pull/200)) |
| Website | `JsonDateInterceptor` (zone-less → UTC); keep `slotStartUk` as text | Sibling [cultpodcasts/website](https://github.com/cultpodcasts/website) `cultpodcasts/src/app/json-date.interceptor.ts` |

## Merged sub-work (already on main)

| Ticket / PR | What landed |
|-------------|-------------|
| RPP #1190 / #1194 / #1217 | Emit `Z`; `UtcJsonDtoContractRules`; east-of-UTC host guard |
| RPP #1219 / #1246 | Replace indexing-path `ToUniversalTime()` misuse |
| RPP #1220 / #1245 | Catalogue `FormatRelease` datetime precision |
| Api #199 / #200 / #204 | OpenAPI `Z` pattern; hero CAS instant compare |
| website #578 / #579 / #580 | JsonDateInterceptor + `Date` typing for API instants |

## Residual in this PR (#1212 close-out)

| Item | Status |
|------|--------|
| HTTP DTO outliers | None remaining under `UtcJsonDtoContractRules` scan |
| Discover CLI `request.Since` | Fixed: `DiscoverCliSince.Resolve` → `UtcDateTimes.ToUtc` (was `ToUniversalTime`) |
| `TweetManager` timeline bounds | Hardened: `UtcDateTimes.ToUtc` / `ToUtcOffset` (Unspecified asserted in `TweetIdRules`) |

## Test matrix (do not delete without replacing)

| Suite | Role |
|-------|------|
| `FunctionHost.Tests` `UtcJsonDtoContractRules` | Every Api DTO/model date-time property serialises with `Z` (`slotStartUk` allowlisted) |
| `UtcJsonDtoContractEastOfUtcHostGuard` | Same under Pacific/Auckland host TZ |
| `UtcJsonHttpResponseRules` | HTTP responses (incl. discovery schedule) |
| `RedditPodcastPoster.Persistence.Tests` `UtcDateTimeSerializationRules` | Cosmos round-trip |
| `CatalogueDtoFormatReleaseRules` | Catalogue release wire shapes |
| Discovery / Spotify / Apple #1219 BusinessRules | Indexing lookback uses UTC-as-unspecified |
| `TweetIdRules` `timeline_start` / `timeline_end` | Tweet timeline bounds treat Unspecified `ReleaseUtc` as UTC |
| `DiscoverCliSinceUtcRules` | Discover CLI `-t`/`--since` Unspecified-as-UTC (+ future reject) |
| Api (sibling) `openapi-schemas.spec.ts` / `openapi-auth.spec.ts` | Worker rejects non-`Z` proxied instants |
| website (sibling) `json-date.interceptor.spec.ts` | Zone-less + `slotStartUk` keep-as-text |

## Agent rule of thumb

Prefer `UtcDateTimes.ToUtc` / `ToUtcOffset` over `ToUniversalTime()`, `SpecifyKind` + bare `new DateTimeOffset(...)`, or implicit conversion. See `AGENTS.md` § Domain conventions.

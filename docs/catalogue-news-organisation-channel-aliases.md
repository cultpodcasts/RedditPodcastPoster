# News organisation channel aliases

**Code (seed):** [`CatalogueNewsOrganisationChannelAliases`](../Class-Libraries/RedditPodcastPoster.Models/Catalogue/CatalogueNewsOrganisationChannelAliases.cs)  
**LookUps:** `CatalogueNewsOrganisationChannelAliasesLookup` via [`ICatalogueCuratorListsProvider`](../Class-Libraries/RedditPodcastPoster.Persistence.Abstractions/Repositories/ICatalogueCuratorListsProvider.cs) (seed fallback until curated)  
**Map:** `OrganisationNameByChannelPublisherName`

## Why it exists

Some news outlets publish on **more than one YouTube channel**, each stored historically as its own Podcast with a different `Name`. For catalogue News, those channels belong under **one canonical NewsOrganisation** name (and ideally one parent document).

`OrganisationNameByChannelPublisherName` maps the **extra channel’s podcast name** → the **canonical organisation name**.

| Extra channel podcast name (key) | Canonical organisation name (value) |
|----------------------------------|-------------------------------------|
| `AP Archive` | `Associated Press` |
| `APT Clips` | `APT` |

Ordinal-ignore-case. Add rows when a curator knows another channel is the same outlet.

Related TV pattern (different purpose — programme title, not outlet merge): [`CatalogueTvShowCanonicalNames`](../Class-Libraries/RedditPodcastPoster.Models/Catalogue/CatalogueTvShowCanonicalNames.cs).

## What it is for

| Use | Behaviour |
|-----|-----------|
| **Catalogue migrate identify** | `IsExtraChannel(podcast.Name)` treats the podcast as a news candidate even when the name does not look like a desk/outlet. |
| **Migrate search swap** | Extra-channel episodes get search `seriesName` = canonical organisation name (`OrganisationNameFor`), so hub/search line up with the outlet people expect. |
| **Kind transfer Podcast → NewsOrganisation** | `ToNewsOrganisation` sets `Name` (and thus fileKey seed) via `OrganisationNameFor(podcast.Name)`. |

Migrate **apply** intent (class comment): parent those reports onto the **canonical** organisation — do **not** mint a second org that only shares the display name. Kind transfer today renames via the alias but still creates a parent at the **podcast’s own id**, so an existing org with the same canonical name can leave you with **two** NewsOrganisations both called e.g. Associated Press.

## What it does *not* do

- It does **not** merge Cosmos parents or move playables between orgs by itself.
- It does **not** change YouTube channel ids. Submit / catalogue parent lookup match on `indexing.youTubeChannelId` (and other service ids), not on display name.
- It does **not** redirect `/news/{old name}`. After a rename, search hubs filter `seriesName eq '{Name}'`, so the old public path goes empty unless something else restores or redirects the name.

## Submit routing (channel id)

After Podcast → News transfer, `CatalogueIndexingSource.FromPodcast` keeps the podcast’s YouTube channel on `NewsOrganisation.Indexing.YouTubeChannelId`.

[`CatalogueParentLookup`](../Class-Libraries/RedditPodcastPoster.UrlSubmission/Services/CatalogueParentLookup.cs) resolves owners by that channel id. So submits from an aliased channel (e.g. AP Archive) attach to the **news-org document that holds that channel id**, even if that document’s `Name` is the canonical alias target (e.g. Associated Press). They do **not** automatically land on a different org that happens to share the same display name but has another channel.

## Operational note (AP Archive, 2026-11)

Transfer of podcast **AP Archive** applied the alias → NewsOrganisation **Associated Press** (`news-associated_press_2`) while a separate Associated Press org (`news-associated_press`) already existed with a different YouTube channel. Public `/news/AP%20Archive` returned zero hits; `/news/Associated%20Press` mixed both orgs’ reports by `seriesName`. Channel id on the transferred org was preserved; submit by channel still targeted that document.

When adding aliases: prefer migrate/apply that **parents onto the canonical id**, or accept an explicit dual-org + distinct public names — do not assume kind transfer alone consolidates outlets.

## Call sites

- `CatalogueParentKindMapper.ToNewsOrganisation`
- `CatalogueMigrateSearchDocuments` (search `seriesName`)
- `CatalogueMigrateIdentify` (`IsExtraChannel`)

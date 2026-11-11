# News organisation: more than one YouTube channel (#1370)

A news organisation can own more than one YouTube channel. The main channel stays in
`indexing.youTubeChannelId` (with its optional `indexing.youTubePlaylistId`). Extra channels go in
`indexing.additionalYouTubeChannelIds`.

```json
"indexing": {
  "youTubeChannelId": "UC…main",
  "youTubePlaylistId": "PL…optional, scopes the main channel only",
  "additionalYouTubeChannelIds": ["UC…archive"]
}
```

## Rules

- **Channel ids only.** An additional channel has no playlist of its own. It is always claimed
  **channel-wide**: the main `youTubePlaylistId` scopes only the main channel.
- **Stored normalised.** Ids are trimmed, blanks and repeats are dropped, and an empty list is not stored
  (the field is omitted in Cosmos and in `indexing` JSON).
- **Only the news organisation PATCH sets it** (`PATCH /newsorganisation/{id}`, body
  `additionalYouTubeChannelIds`). Omitted or `null` leaves the list unchanged, a list replaces it, and `[]`
  clears it. TV shows do not have this field.
- **Validation (400).** Each id must look like a YouTube channel id (`^UC[A-Za-z0-9_-]{22}$`), code
  `invalidAdditionalYouTubeChannelId`. An id equal to the organisation's main channel (after the PATCH)
  is refused with code `additionalYouTubeChannelIsMainChannel`.
- **Uniqueness (409).** An additional channel conflicts like a main channel: with another news
  organisation's or TV show's main or additional channel, and with a channel-wide podcast. Setting a main
  `youTubeChannelId` that another organisation holds as an additional channel is refused the same way.
- **Channel-wide claim (400 `channelWideYouTubeConflict`).** Because an additional channel is
  channel-wide, a podcast, TV show or news organisation that would claim the same channel channel-wide is
  refused, the same as for two channel-wide main channels. A playlist-scoped podcast or TV show may still
  share the channel. This check runs before uniqueness, so a clash with a channel-wide holder is reported
  as this 400 rather than a 409.

## Where it is honoured

| Path | Behaviour |
| --- | --- |
| Hourly indexer (`CatalogueParentsUpdater`) | After the main channel, each additional channel is read as its own channel-wide YouTube-only transient podcast. New videos are saved as `NewsReport`s under the organisation, de-duplicated by YouTube id. Sibling-podcast playlist exclusion runs per channel. Indexer state (playlist cost flags, order) is written back from the main channel only. Logs use the prefix `IndexerCatalogueAdditionalYouTubeChannel`. A pass that skips YouTube skips the additional channels too. |
| URL submit (`CatalogueParentLookup`) | A video on an additional channel routes to the organisation, channel-wide in the playlist tie-break. An organisation that holds any channel (main or additional) is never matched by channel title. |
| Discovery (`EpisodeResultsEnricher`, channel bans) | A result on an additional channel matches the organisation; with `indexAllEpisodes` it is index-all without a playlist lookup. |
| Admin API uniqueness and claim guard | See Rules. |

## Associated Press (#1366): curator steps

The issue's target is one **Associated Press** organisation (`88acfa0c…`) holding both channels: main
`UC52X5wxOL_s5yw0dQk7NtgA` and AP Archive `UCHTK-2W11Vh1V4uwofOfR4w` (today on `ff5bb494…`).

1. On `ff5bb494…`, clear the AP Archive channel: PATCH `{"youTubeChannelId": ""}`. Otherwise step 2 is
   refused naming `ff5bb494…`. That organisation then stops ingesting new videos.
2. On `88acfa0c…`, PATCH `{"additionalYouTubeChannelIds": ["UCHTK-2W11Vh1V4uwofOfR4w"]}`, or add it in the
   website's news organisation edit dialog.
3. From the next indexer pass, new AP Archive videos are stored under `88acfa0c…`.

**Not covered by this change:** moving the existing `ff5bb494…` reports. `NewsReport` is partitioned by
`newsOrganisationId` and there is no tool that moves reports between organisations, rewrites their search
documents, or redirects the old organisation's page and short links. Until a merge tool exists, the old
reports stay under `ff5bb494…`. Removing `ff5bb494…` would hide them. Merging needs a product decision
and a follow-up ticket.

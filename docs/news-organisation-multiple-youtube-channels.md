# News organisation: more than one YouTube channel (#1370)

A news organisation can own more than one YouTube channel, and each channel can optionally be scoped to one of its
own playlists. The main channel stays in `indexing.youTubeChannelId` (with its optional `indexing.youTubePlaylistId`).
Extra channels go in `indexing.additionalYouTubeChannels`. TV shows and podcasts do not have this field.

```json
"indexing": {
  "youTubeChannelId": "UC…main",
  "youTubePlaylistId": "PL…optional, scopes the main channel only",
  "additionalYouTubeChannels": [
    { "youTubeChannelId": "UC…archive" },
    { "youTubeChannelId": "UC…clips", "youTubePlaylistId": "PL…optional, owned by UC…clips",
      "youTubePlaylistQueryIsExpensive": true, "youTubeChannelSearchForbidden": true }
  ]
}
```

`youTubePlaylistQueryIsExpensive` and `youTubeChannelSearchForbidden` on an entry are indexer-owned, as the same flags
on the main channel are. The curator API never sets them.

## Rules

- **Per-channel playlist.** An entry without `youTubePlaylistId` is claimed **channel-wide**. An entry with one is
  scoped to that playlist on that channel only. The main `youTubePlaylistId` never scopes an additional channel, and an
  additional channel's playlist never scopes the main channel (`CatalogueIndexingSource.YouTubePlaylistIdOn(channelId)`).
- **Stored normalised.** Ids are trimmed, an entry with a blank channel id is dropped, a repeated channel keeps its
  first entry, and an empty list is not stored (the field is omitted in Cosmos and in `indexing` JSON).
- **Only the news organisation PATCH sets it** (`PATCH /newsorganisation/{id}`):
  - `additionalYouTubeChannels: [{ "youTubeChannelId": "UC…", "youTubePlaylistId": "PL…" | null }]`, or
  - the older `additionalYouTubeChannelIds: ["UC…"]` (each id stored channel-wide, no playlist).

  Omitted or `null` leaves the list unchanged, a list replaces it, and `[]` clears it. Sending both fields is refused.
  A channel that stays keeps its indexer flags (`youTubePlaylistQueryIsExpensive` only while its playlist is unchanged).
- **GET** returns `additionalYouTubeChannels: [{ youTubeChannelId, youTubePlaylistId | null }]` and, for older clients,
  `additionalYouTubeChannelIds` (the same channel ids, in order).
- **Validation (400).**
  - `invalidAdditionalYouTubeChannelId`: an id that is not `^UC[A-Za-z0-9_-]{22}$`, an entry with a playlist but no
    channel id, the same channel listed twice with different playlists, or both body fields sent.
  - `additionalYouTubeChannelIsMainChannel`: an id equal to the organisation's main channel (after the PATCH).
  - A new or changed playlist must belong to its channel (one `playlists.list` call; an unchanged playlist costs no
    quota), with the main-channel playlist codes: `youTubePlaylistIdInvalid`, `youTubePlaylistNotFound`,
    `youTubePlaylistChannelMismatch`. A lookup that fails is **503** `youTubePlaylistLookupFailed`.
- **Uniqueness (409).** An additional channel conflicts like a main channel, whatever playlist scopes it: with another
  news organisation's or TV show's main or additional channel, and with a channel-wide podcast. A playlist-scoped
  podcast on the same channel is allowed (submission routes by playlist membership). Setting a main
  `youTubeChannelId` that another organisation holds as an additional channel is refused the same way.
- **Channel-wide claim (400 `channelWideYouTubeConflict`).** A channel-wide additional channel is refused when a
  podcast, TV show or news organisation already claims the same channel channel-wide, the same as for two channel-wide
  main channels. A playlist-scoped additional channel makes no channel-wide claim. This check runs before uniqueness.

## Where it is honoured

| Path | Behaviour |
| --- | --- |
| Hourly indexer (`CatalogueParentsUpdater`) | After the main channel, each additional channel is read as its own YouTube-only transient podcast: channel-wide, or through its playlist (owner = that channel). New videos are saved as `NewsReport`s under the organisation, de-duplicated by YouTube id. Sibling-podcast playlist exclusion runs per channel-wide channel. Each channel's `youTubeChannelSearchForbidden` / `youTubePlaylistQueryIsExpensive` is written back onto its entry; the patch of `indexing.additionalYouTubeChannels` is guarded so it only applies while the stored channel and playlist ids are unchanged (a curator edit mid-pass wins, and the next pass measures again). Playlist history, order and large-playlist hints stay main-channel only. Logs use the prefix `IndexerCatalogueAdditionalYouTubeChannel`. A pass that skips YouTube skips the additional channels too. |
| URL submit (`CatalogueParentLookup`, `PodcastService`) | A video on an additional channel routes to the organisation. With a playlist on that channel, the playlist-membership tie-breaks use that playlist (never the main one): a playlist-scoped podcast containing the video keeps it, and the organisation's playlist containing it wins over a channel-wide podcast, exactly as the main channel's playlist does. An organisation that holds any channel (main or additional) is never matched by channel title. |
| Discovery (`EpisodeResultsEnricher`, channel bans) | A result on an additional channel matches the organisation, and reports that channel's playlist. With `indexAllEpisodes` it is index-all when the channel is channel-wide or the video is in that channel's playlist. |
| Admin API uniqueness and claim guard | See Rules. |

## Associated Press (#1366): curator steps

The issue's target is one **Associated Press** organisation (`88acfa0c…`) holding both channels: main
`UC52X5wxOL_s5yw0dQk7NtgA` and AP Archive `UCHTK-2W11Vh1V4uwofOfR4w` (today on `ff5bb494…`).

1. On `ff5bb494…`, clear the AP Archive channel: PATCH `{"youTubeChannelId": ""}`. Otherwise step 2 is
   refused naming `ff5bb494…`. That organisation then stops ingesting new videos.
2. On `88acfa0c…`, PATCH `{"additionalYouTubeChannels": [{"youTubeChannelId": "UCHTK-2W11Vh1V4uwofOfR4w"}]}`
   (add `"youTubePlaylistId"` to index only one AP Archive playlist), or add it in the website's news organisation
   edit dialog.
3. From the next indexer pass, new AP Archive videos are stored under `88acfa0c…`.

**Not covered by this change:** moving the existing `ff5bb494…` reports. `NewsReport` is partitioned by
`newsOrganisationId` and there is no tool that moves reports between organisations, rewrites their search
documents, or redirects the old organisation's page and short links. Until a merge tool exists, the old
reports stay under `ff5bb494…`. Removing `ff5bb494…` would hide them. Merging needs a product decision
and a follow-up ticket.

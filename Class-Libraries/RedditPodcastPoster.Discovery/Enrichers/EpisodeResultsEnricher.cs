using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Discovery.Models;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.PodcastServices.YouTube.Clients;
using RedditPodcastPoster.PodcastServices.YouTube.Playlist;
using RedditPodcastPoster.PodcastServices.YouTube.Resolvers;
using Podcast = RedditPodcastPoster.Models.Podcasts.Podcast;

namespace RedditPodcastPoster.Discovery.Enrichers;

/// <summary>
/// Marks each discovered episode with the catalogue owners of its Spotify show, Apple podcast and YouTube channel: the
/// podcast, and the news organisation / TV show whose <c>indexing</c> holds the id (a migrated podcast keeps its id,
/// so <c>matchingPodcastIds</c> carries the parent's id as it carried the podcast's). An owner that indexes all
/// episodes eliminates the result downstream, because the indexer already picks those episodes up.
/// <para>
/// When several owners share a YouTube channel, a channel-wide owner (no playlist) is preferred over a playlist-scoped
/// one, the same tie-break as <c>CatalogueParentLookup.FindByOwner</c>. A playlist-scoped owner eliminates only a
/// video that is in its playlist; any other video on the channel stays matched and is not index-all.
/// </para>
/// </summary>
public class EpisodeResultsEnricher(
    IEpisodeResultEnricher episodeResultEnricher,
    IPodcastRepository podcastRepository,
    INewsOrganisationRepository newsOrganisationRepository,
    ITvShowRepository tvShowRepository,
    IYouTubePlaylistMembership youTubePlaylistMembership,
    IYouTubeServiceWrapper youTubeService,
    ILogger<EpisodeResultsEnricher> logger
) : IEpisodeResultsEnricher
{
    public async IAsyncEnumerable<EnrichedEpisodeResult> EnrichWithPodcastDetails(
        IEnumerable<EpisodeResult> episodeResults)
    {
        // News organisations / TV shows are few: load the indexed ones once per run and match in memory.
        IReadOnlyList<CatalogueOwner>? catalogueOwners = null;
        foreach (var episodeResult in episodeResults)
        {
            var podcasts = new List<Podcast>();
            var parents = new List<CatalogueOwner>();
            // YouTube-selected owners only. A Spotify / Apple match that also has a playlist must keep its index-all.
            var youTubePlaylistByOwner = new Dictionary<Guid, string?>();
            if (episodeResult.PodcastIds.Spotify is { } spotifyId && !string.IsNullOrWhiteSpace(spotifyId))
            {
                AddMatches(podcasts, await podcastRepository.GetAllBy(x => x.SpotifyId == spotifyId).ToArrayAsync());
                catalogueOwners ??= await LoadCatalogueOwners();
                Add(parents, FindParent(catalogueOwners, x => x.SpotifyId == spotifyId));
            }

            if (episodeResult.PodcastIds.Apple is { } appleId)
            {
                AddMatches(podcasts, await podcastRepository.GetAllBy(x => x.AppleId == appleId).ToArrayAsync());
                catalogueOwners ??= await LoadCatalogueOwners();
                Add(parents, FindParent(catalogueOwners, x => x.AppleId == appleId));
            }

            if (episodeResult.PodcastIds.YouTube is { } channelId && !string.IsNullOrWhiteSpace(channelId))
            {
                var channelPodcasts = await podcastRepository.GetAllBy(x => x.YouTubeChannelId == channelId)
                    .ToArrayAsync();
                var indexed = GetCurrentIndexed(PreferChannelWide(channelPodcasts, x => x.YouTubePlaylistId));
                if (Add(podcasts, indexed))
                {
                    youTubePlaylistByOwner[indexed!.Id] = indexed.YouTubePlaylistId;
                }

                var removedOutlet = channelPodcasts.Any(x =>
                    x.IsRemoved() && string.IsNullOrEmpty(x.YouTubePlaylistId));
                foreach (var match in channelPodcasts)
                {
                    if (match.IsRemoved() ||
                        (removedOutlet && !string.IsNullOrEmpty(match.YouTubePlaylistId)))
                    {
                        if (Add(podcasts, match))
                        {
                            youTubePlaylistByOwner[match.Id] = match.YouTubePlaylistId;
                        }
                    }
                }

                catalogueOwners ??= await LoadCatalogueOwners();
                var parent = FindYouTubeParent(catalogueOwners, channelId);
                if (Add(parents, parent))
                {
                    youTubePlaylistByOwner[parent!.Id] = parent.Indexing.YouTubePlaylistId;
                }
            }

            var enriched = episodeResultEnricher.Enrich(episodeResult, podcasts.ToArray());
            var combined = parents.Count == 0
                ? enriched.PodcastResults
                : enriched.PodcastResults
                    .Concat(parents
                        .Where(parent => enriched.PodcastResults.All(x => x.PodcastId != parent.Id))
                        .Select(parent => new PodcastResult(
                            parent.Id,
                            parent.IndexAllEpisodes,
                            parent.Removed,
                            parent.Indexing.YouTubePlaylistId ?? string.Empty,
                            suppressesUnrelatedShows: parent.Removed)))
                    .ToArray();
            yield return new EnrichedEpisodeResult(
                enriched.EpisodeResult,
                await ApplyPlaylistScope(episodeResult, combined, youTubePlaylistByOwner));
        }
    }

    /// <summary>
    /// Keeps the indexed owner the elimination rule already used, every removed match, and every live
    /// playlist-scoped show. A removed channel-wide outlet stays visible beside an indexed row of the same
    /// show, and a live playlist show on that channel stays visible so the outlet does not own it.
    /// </summary>
    private void AddMatches(List<Podcast> podcasts, Podcast[] matches)
    {
        Add(podcasts, GetCurrentIndexed(matches));
        foreach (var match in matches)
        {
            if (match.IsRemoved() || !string.IsNullOrEmpty(match.YouTubePlaylistId))
            {
                Add(podcasts, match);
            }
        }
    }

    /// <summary>
    /// Channel-wide owners (no playlist) win when several share a channel, as
    /// <c>CatalogueParentLookup.FindByOwner</c> does. Playlist-scoped owners stay only when every match is playlist-scoped.
    /// </summary>
    private static T[] PreferChannelWide<T>(T[] matches, Func<T, string?> playlistId)
    {
        if (matches.Length <= 1)
        {
            return matches;
        }

        var channelWide = matches.Where(x => string.IsNullOrWhiteSpace(playlistId(x))).ToArray();
        return channelWide.Length > 0 ? channelWide : matches;
    }

    private async Task<PodcastResult[]> ApplyPlaylistScope(
        EpisodeResult episode,
        PodcastResult[] results,
        Dictionary<Guid, string?> youTubePlaylistByOwner)
    {
        if (youTubePlaylistByOwner.Count == 0 || results.All(x => !x.IndexAllEpisodes))
        {
            return results;
        }

        var reported = new PodcastResult[results.Length];
        for (var i = 0; i < results.Length; i++)
        {
            var result = results[i];
            if (result.IndexAllEpisodes &&
                youTubePlaylistByOwner.TryGetValue(result.PodcastId, out var playlistId) &&
                !string.IsNullOrWhiteSpace(playlistId) &&
                !await VideoIsInPlaylist(playlistId, episode))
            {
                logger.LogInformation(
                    "Matched playlist-scoped owner '{OwnerId}' but the video is not in playlist '{PlaylistId}', so it is not index-all.",
                    result.PodcastId, playlistId);
                reported[i] = new PodcastResult(
                    result.PodcastId,
                    false,
                    result.Removed,
                    result.YouTubePlaylistId,
                    result.SuppressesUnrelatedShows);
            }
            else
            {
                reported[i] = result;
            }
        }

        return reported;
    }

    /// <summary>
    /// A <c>list=</c> URL of this playlist needs no API call. Otherwise one playlist-items lookup. Unknown is not
    /// membership: the video stays discoverable.
    /// </summary>
    private async Task<bool> VideoIsInPlaylist(string playlistId, EpisodeResult episode)
    {
        var url = episode.Urls.YouTube;
        if (url == null)
        {
            return false;
        }

        if (string.Equals(YouTubePlaylistIdResolver.Extract(url), playlistId, StringComparison.Ordinal))
        {
            return true;
        }

        var videoId = YouTubeIdResolver.Extract(url);
        if (string.IsNullOrWhiteSpace(videoId))
        {
            return false;
        }

        return await youTubePlaylistMembership.Contains(youTubeService, playlistId, videoId) == true;
    }

    private static bool Add(List<Podcast> podcasts, Podcast? podcast)
    {
        if (podcast != null && podcasts.All(x => x.Id != podcast.Id))
        {
            podcasts.Add(podcast);
            return true;
        }

        return false;
    }

    private static bool Add(List<CatalogueOwner> parents, CatalogueOwner? parent)
    {
        if (parent != null && parents.All(x => x.Id != parent.Id))
        {
            parents.Add(parent);
            return true;
        }

        return false;
    }

    private async Task<IReadOnlyList<CatalogueOwner>> LoadCatalogueOwners()
    {
        var organisations = await newsOrganisationRepository.GetAllBy(x => x.Indexing != null).ToArrayAsync();
        var shows = await tvShowRepository.GetAllBy(x => x.Indexing != null).ToArrayAsync();
        return
        [
            .. organisations.Select(x => new CatalogueOwner(x.Id, x.Indexing!, x.IsRemoved())),
            .. shows.Select(x => new CatalogueOwner(x.Id, x.Indexing!, x.IsRemoved()))
        ];
    }

    /// <summary>The news organisation / TV show that owns the id, with the podcast tie-break (the auto-indexed one).</summary>
    private static CatalogueOwner? FindParent(
        IReadOnlyList<CatalogueOwner> owners,
        Func<CatalogueIndexingSource, bool> owns)
    {
        var matches = owners.Where(x => owns(x.Indexing)).ToArray();
        return FirstAutoOrSingle(matches, x => x.Indexing.IsAutoIndexed);
    }

    private static CatalogueOwner? FindYouTubeParent(IReadOnlyList<CatalogueOwner> owners, string channelId)
    {
        var matches = owners.Where(x => x.Indexing.YouTubeChannelId == channelId).ToArray();
        return FirstAutoOrSingle(
            PreferChannelWide(matches, x => x.Indexing.YouTubePlaylistId),
            x => x.Indexing.IsAutoIndexed);
    }

    private static T? FirstAutoOrSingle<T>(T[] matches, Func<T, bool> isAutoIndexed) where T : class
    {
        return matches.Length switch
        {
            0 => null,
            1 => matches[0],
            _ => matches.FirstOrDefault(isAutoIndexed)
        };
    }

    private Podcast? GetCurrentIndexed(Podcast[] podcasts) =>
        FirstAutoOrSingle(
            podcasts,
            x => x.IndexAllEpisodes || !string.IsNullOrEmpty(x.EpisodeIncludeTitleRegex));

    private sealed record CatalogueOwner(Guid Id, CatalogueIndexingSource Indexing, bool Removed)
    {
        public bool IndexAllEpisodes => Indexing.IndexAllEpisodes;
    }
}

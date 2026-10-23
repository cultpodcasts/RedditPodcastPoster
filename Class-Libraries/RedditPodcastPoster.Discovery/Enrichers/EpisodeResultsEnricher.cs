using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Discovery.Models;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using Podcast = RedditPodcastPoster.Models.Podcasts.Podcast;

namespace RedditPodcastPoster.Discovery.Enrichers;

/// <summary>
/// Marks each discovered episode with the catalogue owners of its Spotify show, Apple podcast and YouTube channel: the
/// podcast, and the news organisation / TV show whose <c>indexing</c> holds the id (a migrated podcast keeps its id,
/// so <c>matchingPodcastIds</c> carries the parent's id as it carried the podcast's). An owner that indexes all
/// episodes eliminates the result downstream, because the indexer already picks those episodes up.
/// </summary>
public class EpisodeResultsEnricher(
    IEpisodeResultEnricher episodeResultEnricher,
    IPodcastRepository podcastRepository,
    INewsOrganisationRepository newsOrganisationRepository,
    ITvShowRepository tvShowRepository,
#pragma warning disable CS9113 // Parameter is unread.
    ILogger<EpisodeResultsEnricher> Logger
#pragma warning restore CS9113 // Parameter is unread.
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
                AddMatches(podcasts,
                    await podcastRepository.GetAllBy(x => x.YouTubeChannelId == channelId).ToArrayAsync());
                catalogueOwners ??= await LoadCatalogueOwners();
                Add(parents, FindParent(catalogueOwners, x => x.YouTubeChannelId == channelId));
            }

            var enriched = episodeResultEnricher.Enrich(episodeResult, podcasts.ToArray());
            yield return parents.Count == 0
                ? enriched
                : new EnrichedEpisodeResult(
                    enriched.EpisodeResult,
                    enriched.PodcastResults
                        .Concat(parents
                            .Where(parent => enriched.PodcastResults.All(x => x.PodcastId != parent.Id))
                            .Select(parent => new PodcastResult(parent.Id, parent.IndexAllEpisodes, parent.Removed)))
                        .ToArray());
        }
    }

    /// <summary>
    /// Keeps the indexed owner the elimination rule already used, and also every removed match, so a banned
    /// channel is visible to that same rule even when another podcast on the id is the indexed one.
    /// </summary>
    private void AddMatches(List<Podcast> podcasts, Podcast[] matches)
    {
        Add(podcasts, GetCurrentIndexed(matches));
        foreach (var removed in matches)
        {
            if (removed.IsRemoved())
            {
                Add(podcasts, removed);
            }
        }
    }

    private static void Add(List<Podcast> podcasts, Podcast? podcast)
    {
        if (podcast != null && podcasts.All(x => x.Id != podcast.Id))
        {
            podcasts.Add(podcast);
        }
    }

    private static void Add(List<CatalogueOwner> parents, CatalogueOwner? parent)
    {
        if (parent != null && parents.All(x => x.Id != parent.Id))
        {
            parents.Add(parent);
        }
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
        return matches.Length switch
        {
            0 => null,
            1 => matches[0],
            _ => matches.FirstOrDefault(x => x.Indexing.IsAutoIndexed)
        };
    }

    private Podcast? GetCurrentIndexed(Podcast[] podcasts)
    {
        if (podcasts.Length == 0)
        {
            return null;
        }

        if (podcasts.Length == 1)
        {
            return podcasts.Single();
        }

        return podcasts.FirstOrDefault(x => x.IndexAllEpisodes || !string.IsNullOrEmpty(x.EpisodeIncludeTitleRegex));
    }

    private sealed record CatalogueOwner(Guid Id, CatalogueIndexingSource Indexing, bool Removed)
    {
        public bool IndexAllEpisodes => Indexing.IndexAllEpisodes;
    }
}

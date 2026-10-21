using System.Linq.Expressions;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Discovery.Models;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.News;
using RedditPodcastPoster.Models.TvShows;
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
        foreach (var episodeResult in episodeResults)
        {
            var podcasts = new List<Podcast>();
            var parents = new List<CatalogueOwner>();
            if (episodeResult.PodcastIds.Spotify is { } spotifyId)
            {
                Add(podcasts, GetCurrentIndexed(
                    await podcastRepository.GetAllBy(x => x.SpotifyId == spotifyId).ToArrayAsync()));
                Add(parents, await FindParent(
                    x => x.Indexing != null && x.Indexing.SpotifyId == spotifyId,
                    x => x.Indexing != null && x.Indexing.SpotifyId == spotifyId));
            }

            if (episodeResult.PodcastIds.Apple is { } appleId)
            {
                Add(podcasts, GetCurrentIndexed(
                    await podcastRepository.GetAllBy(x => x.AppleId == appleId).ToArrayAsync()));
                Add(parents, await FindParent(
                    x => x.Indexing != null && x.Indexing.AppleId == appleId,
                    x => x.Indexing != null && x.Indexing.AppleId == appleId));
            }

            if (episodeResult.PodcastIds.YouTube is { } channelId)
            {
                Add(podcasts, GetCurrentIndexed(
                    await podcastRepository.GetAllBy(x => x.YouTubeChannelId == channelId).ToArrayAsync()));
                Add(parents, await FindParent(
                    x => x.Indexing != null && x.Indexing.YouTubeChannelId == channelId,
                    x => x.Indexing != null && x.Indexing.YouTubeChannelId == channelId));
            }

            var enriched = episodeResultEnricher.Enrich(episodeResult, podcasts.ToArray());
            yield return parents.Count == 0
                ? enriched
                : new EnrichedEpisodeResult(
                    enriched.EpisodeResult,
                    enriched.PodcastResults
                        .Concat(parents
                            .Where(parent => enriched.PodcastResults.All(x => x.PodcastId != parent.Id))
                            .Select(parent => new PodcastResult(parent.Id, parent.IndexAllEpisodes)))
                        .ToArray());
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

    /// <summary>The news organisation / TV show that owns the id, with the podcast tie-break (the auto-indexed one).</summary>
    private async Task<CatalogueOwner?> FindParent(
        Expression<Func<NewsOrganisation, bool>> organisationOwns,
        Expression<Func<TvShow, bool>> showOwns)
    {
        var organisations = await newsOrganisationRepository.GetAllBy(organisationOwns).ToArrayAsync();
        var shows = await tvShowRepository.GetAllBy(showOwns).ToArrayAsync();
        CatalogueOwner[] owners =
        [
            .. organisations.Select(x => new CatalogueOwner(x.Id, x.Indexing!)),
            .. shows.Select(x => new CatalogueOwner(x.Id, x.Indexing!))
        ];
        return owners.Length switch
        {
            0 => null,
            1 => owners[0],
            _ => owners.FirstOrDefault(x => x.Indexing.IsAutoIndexed)
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

    private sealed record CatalogueOwner(Guid Id, CatalogueIndexingSource Indexing)
    {
        public bool IndexAllEpisodes => Indexing.IndexAllEpisodes;
    }
}

using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Episodes.Adapters;
using RedditPodcastPoster.Episodes.Adapters.Inputs;
using RedditPodcastPoster.Episodes.Factories;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Apple.Mapping;

namespace RedditPodcastPoster.PodcastServices.Apple;

public class AppleEpisodeProvider(
    ICachedApplePodcastService applePodcastService,
    IEpisodeCatalogueAdapter<AppleCatalogueInput> appleEpisodeAdapter,
    IEpisodeFromCandidateFactory episodeFromCandidateFactory,
#pragma warning disable CS9113 // Parameter is unread.
    ILogger<AppleEpisodeProvider> logger)
#pragma warning restore CS9113 // Parameter is unread.
    : IAppleEpisodeProvider
{
    public async Task<IList<Episode>?> GetEpisodes(ApplePodcastId podcastId, IndexingContext indexingContext)
    {
        var episodes = await applePodcastService.GetEpisodes(podcastId, indexingContext);

        if (episodes == null)
        {
            return null;
        }

        if (indexingContext.ReleasedSince.HasValue)
        {
            episodes = episodes.Where(x => x.Release >= indexingContext.ReleasedSince.Value).ToList();
        }

        return episodes.Select(MapEpisode).ToList();
    }

    private Episode MapEpisode(AppleEpisode episode)
    {
        var candidate = appleEpisodeAdapter.Adapt(episode.ToCatalogueInput());
        return episodeFromCandidateFactory.Create(candidate, episode.Explicit);
    }
}

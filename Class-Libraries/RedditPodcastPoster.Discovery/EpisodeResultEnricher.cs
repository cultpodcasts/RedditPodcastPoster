using RedditPodcastPoster.PodcastServices.Abstractions;
using Podcast = RedditPodcastPoster.Models.V2.Podcast;

namespace RedditPodcastPoster.Discovery;

public class EpisodeResultEnricher : IEpisodeResultEnricher
{
    public EnrichedEpisodeResult Enrich(EpisodeResult episodeResult, Podcast[] podcasts)
    {
        var podcastResults = podcasts.Select(x => new PodcastResult(x.Id, x.IndexAllEpisodes));
        return new EnrichedEpisodeResult(episodeResult, podcastResults.ToArray());
    }
}
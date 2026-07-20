using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using Podcast = RedditPodcastPoster.Models.Podcast;

namespace RedditPodcastPoster.Discovery;

public class EpisodeResultEnricher : IEpisodeResultEnricher
{
    public EnrichedEpisodeResult Enrich(EpisodeResult episodeResult, Podcast[] podcasts)
    {
        var podcastResults = podcasts.Select(x => new PodcastResult(x.Id, x.IndexAllEpisodes));
        return new EnrichedEpisodeResult(episodeResult, podcastResults.ToArray());
    }
}

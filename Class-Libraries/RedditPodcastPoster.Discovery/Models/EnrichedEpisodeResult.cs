using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;

namespace RedditPodcastPoster.Discovery.Models;

public class EnrichedEpisodeResult(EpisodeResult episodeResult, PodcastResult[] podcastResults)
{
    public EpisodeResult EpisodeResult { get; } = episodeResult;
    public PodcastResult[] PodcastResults { get; } = podcastResults;
}

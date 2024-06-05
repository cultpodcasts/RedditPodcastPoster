using RedditPodcastPoster.PodcastServices.Abstractions;

namespace RedditPodcastPoster.Discovery;

public class EnrichedEpisodeResult(EpisodeResult episodeResult, PodcastResult[] podcastResults)
{
    public EpisodeResult EpisodeResult { get; } = episodeResult;
    public PodcastResult[] PodcastResults { get; } = podcastResults;
}
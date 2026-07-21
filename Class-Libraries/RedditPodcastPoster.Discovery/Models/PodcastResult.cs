namespace RedditPodcastPoster.Discovery.Models;

public class PodcastResult(Guid podcastId, bool indexAllEpisodes)
{
    public Guid PodcastId { get; } = podcastId;
    public bool IndexAllEpisodes { get; } = indexAllEpisodes;
}
namespace RedditPodcastPoster.Discovery.Models;

public class PodcastResult(Guid podcastId, bool indexAllEpisodes, bool removed = false)
{
    public Guid PodcastId { get; } = podcastId;
    public bool IndexAllEpisodes { get; } = indexAllEpisodes;
    public bool Removed { get; } = removed;
}
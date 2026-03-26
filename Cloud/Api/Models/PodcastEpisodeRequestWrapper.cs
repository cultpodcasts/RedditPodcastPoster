namespace Api.Models;

public class PodcastEpisodeRequestWrapper(Guid episodeId)
{
    public readonly Guid? PodcastId;
    public readonly string? PodcastName;
    public readonly Guid EpisodeId = episodeId;

    public PodcastEpisodeRequestWrapper(string podcastName, Guid episodeId) : this(episodeId)
    {
        PodcastName = podcastName;
    }

    public PodcastEpisodeRequestWrapper(Guid? podcastId, Guid episodeId) : this(episodeId)
    {
        PodcastId = podcastId;
    }
}
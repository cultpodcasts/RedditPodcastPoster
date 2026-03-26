namespace Api.Models;

public class PodcastGetRequest
{
    private readonly string? podcastName;

    public PodcastGetRequest(string podcastName, Guid? episodeId)
    {
        this.podcastName = podcastName;
        EpisodeId = episodeId;
    }

    public PodcastGetRequest(Guid podcastId)
    {
        PodcastId = podcastId;
    }

    public string? PodcastName => podcastName == null ? null : Uri.UnescapeDataString(podcastName);
    public Guid? EpisodeId { get; init; }
    public Guid? PodcastId { get; init; }

    public override string ToString()
    {
        if (PodcastId != null)
        {
            return $"PodcastId: '{PodcastId}'.";
        }

        return $"PodcastName: '{PodcastName}', EpisodeId: '{EpisodeId}'.";
    }
}
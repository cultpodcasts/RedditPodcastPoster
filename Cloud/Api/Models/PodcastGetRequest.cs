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

    /// <summary>
    /// Builds a get-request from a route segment that may be a podcast id or name.
    /// Guid segments resolve by id (episode id is unused); names may include episode id for disambiguation.
    /// </summary>
    public static PodcastGetRequest FromRouteIdentifier(string podcastIdentifier, Guid? episodeId = null)
    {
        if (Guid.TryParse(podcastIdentifier, out var podcastId))
        {
            return new PodcastGetRequest(podcastId);
        }

        return new PodcastGetRequest(podcastIdentifier, episodeId);
    }

    public string? PodcastName => podcastName == null ? null : PodcastRouteNameNormalizer.Normalize(podcastName);
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

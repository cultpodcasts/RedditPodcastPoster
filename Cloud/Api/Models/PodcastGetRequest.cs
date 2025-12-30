using System.Net;

namespace Api.Models;

public class PodcastGetRequest(string podcastName, Guid? episodeId) 
{
    public string PodcastName => Uri.UnescapeDataString(podcastName);
    public Guid? EpisodeId { get; init; } = episodeId;
}
using System.Net;

namespace Api.Models;

public class PodcastGetRequest(string podcastName, Guid? EpisodeId) 
{
    public string PodcastName => WebUtility.UrlDecode(podcastName);
    public Guid? EpisodeId { get; init; }
}
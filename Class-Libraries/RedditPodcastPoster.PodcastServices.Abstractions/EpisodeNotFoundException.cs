using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.PodcastServices.Abstractions;

public class EpisodeNotFoundException(string episodeId, Service service)
    : Exception($"{service} episode with {service} episode-id '{episodeId}' not found")
{
    public EpisodeNotFoundException(long episodeId, Service service) : this(episodeId.ToString(), service)
    {
    }
}
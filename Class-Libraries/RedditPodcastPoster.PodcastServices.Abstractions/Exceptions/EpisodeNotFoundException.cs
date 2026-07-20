using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.PodcastServices.Abstractions.Exceptions;

public class EpisodeNotFoundException(string spotifyId, Service service)
    : Exception($"{service} episode with {service} episode-id '{spotifyId}' not found");
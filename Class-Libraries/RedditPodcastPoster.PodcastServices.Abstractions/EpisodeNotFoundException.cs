namespace RedditPodcastPoster.PodcastServices.Abstractions;

public class EpisodeNotFoundException(string spotifyId)
    : Exception($"Spotify episode with spotify episode-id '{spotifyId}' not found");
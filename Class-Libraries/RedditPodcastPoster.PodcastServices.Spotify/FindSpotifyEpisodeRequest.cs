namespace RedditPodcastPoster.PodcastServices.Spotify;

public record FindSpotifyEpisodeRequest(
    string PodcastSpotifyId, 
    string PodcastName, 
    string EpisodeSpotifyId,
    string EpisodeTitle,
    DateTime? Released,
    bool HasExpensiveSpotifyEpisodesQuery,
    string? Market=null);
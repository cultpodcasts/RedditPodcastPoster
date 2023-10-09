namespace RedditPodcastPoster.Common.PodcastServices.Spotify;

public record FindSpotifyEpisodeRequest(
    string PodcastSpotifyId, 
    string PodcastName, 
    string EpisodeSpotifyId,
    string EpisodeTitle);
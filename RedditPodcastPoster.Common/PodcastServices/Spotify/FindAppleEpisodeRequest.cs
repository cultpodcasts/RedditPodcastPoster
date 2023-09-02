namespace RedditPodcastPoster.Common.PodcastServices.Spotify;

public record FindAppleEpisodeRequest(
    long? PodcastAppleId,
    string PodcastName,
    long? EpisodeAppleId,
    string EpisodeTitle,
    DateTime Released,
    int EpisodeIndex);
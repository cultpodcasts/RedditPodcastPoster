namespace RedditPodcastPoster.Common.PodcastServices.Apple;

public record FindAppleEpisodeRequest(
    long? PodcastAppleId,
    string PodcastName,
    long? EpisodeAppleId,
    string EpisodeTitle,
    DateTime Released,
    int EpisodeIndex);
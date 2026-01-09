using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.PodcastServices.Apple;

public record FindAppleEpisodeRequest(
    long? PodcastAppleId,
    string PodcastName,
    long? EpisodeAppleId,
    string EpisodeTitle,
    DateTime? Released,
    Service? ReleaseAuthority,
    TimeSpan? EpisodeLength,
    TimeSpan? YouTubePublishingDelay);
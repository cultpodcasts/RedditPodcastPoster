using RedditPodcastPoster.Models.Podcasts;

namespace RedditPodcastPoster.PodcastServices.Apple.Models;

public record FindAppleEpisodeRequest(
    long? PodcastAppleId,
    string PodcastName,
    long? EpisodeAppleId,
    string EpisodeTitle,
    DateTime? Released,
    Service? ReleaseAuthority,
    TimeSpan? EpisodeLength,
    TimeSpan? YouTubePublishingDelay,
    bool EnrichingYouTubeDiscoveredEpisode = false,
    string? EpisodeDescription = null,
    string? DefaultSubject = null,
    IReadOnlyList<string>? IgnoredSubjects = null,
    string? Language = null);

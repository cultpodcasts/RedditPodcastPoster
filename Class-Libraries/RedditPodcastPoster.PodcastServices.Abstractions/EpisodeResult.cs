namespace RedditPodcastPoster.PodcastServices.Abstractions;

public record EpisodeResult(
    string Id,
    DateTime Released,
    string Description,
    string EpisodeName,
    TimeSpan? Length,
    string ShowName,
    DiscoverService DiscoverService,
    Uri? Url = null,
    string? ServicePodcastId = null,
    ulong? ViewCount = null,
    ulong? MemberCount = null);
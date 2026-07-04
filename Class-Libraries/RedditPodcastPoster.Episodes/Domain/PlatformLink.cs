using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Episodes.Domain;

public sealed record PlatformLink(
    Service Service,
    string? Id,
    Uri? Url,
    Uri? Image);

namespace RedditPodcastPoster.PodcastServices.Abstractions;

public record NonPodcastServiceItemMetaData(
    string Title,
    string Description,
    TimeSpan? Duration = null,
    DateTime? Release = null,
    Uri? Image = null,
    bool? Explicit = null);
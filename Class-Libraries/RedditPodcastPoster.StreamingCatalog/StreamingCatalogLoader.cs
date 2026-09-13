using RedditPodcastPoster.PodcastServices.Abstractions.Streaming;

namespace RedditPodcastPoster.StreamingCatalog;

/// <summary>
/// Loads provider registrations into <see cref="StreamingServiceCatalog"/>.
/// Referencing this type (or calling <see cref="EnsureLoaded"/>) loads this assembly
/// so the module initializer can populate the composed catalog.
/// </summary>
public static class StreamingCatalogLoader
{
    public static void EnsureLoaded() =>
        StreamingServiceCatalog.Use(
            KnownStreamingServices.All,
            KnownStreamingServices.ImageCoalesceStreamingKeys);
}

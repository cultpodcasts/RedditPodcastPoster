using RedditPodcastPoster.PodcastServices.Abstractions.Streaming;

namespace RedditPodcastPoster.StreamingCatalog;

/// <summary>
/// Loads provider registrations into <see cref="StreamingServiceCatalog"/>.
/// Referencing this type (or calling <see cref="EnsureLoaded"/>) loads this assembly
/// so the module initializer can populate the composed catalog.
/// </summary>
/// <remarks>
/// Hosts that call <c>SearchEncodedKeys</c>, <c>TryCompactUrl</c>, or <c>TryResolveKey</c>
/// must load this assembly. The StreamingCatalog module initializer runs when a type from
/// this assembly is first used. Isolated test assemblies should call <see cref="EnsureLoaded"/>
/// from a <c>ModuleInitializer</c> — <c>Episodes.TestSupport</c> covers tests that reference it.
/// Function apps and consoles that only need <c>StreamingServiceKeys</c> constants should
/// reference PodcastServices.Abstractions instead of this project.
/// </remarks>
public static class StreamingCatalogLoader
{
    public static void EnsureLoaded() =>
        StreamingServiceCatalog.Use(
            KnownStreamingServices.All,
            KnownStreamingServices.ImageCoalesceStreamingKeys);
}

#pragma warning disable CA2255 // ModuleInitializer is the load hook for the composed streaming catalog.
using System.Runtime.CompilerServices;
using RedditPodcastPoster.StreamingCatalog;

namespace RedditPodcastPoster.PodcastServices;

internal static class StreamingCatalogLoad
{
    [ModuleInitializer]
    internal static void Load() => StreamingCatalogLoader.EnsureLoaded();
}

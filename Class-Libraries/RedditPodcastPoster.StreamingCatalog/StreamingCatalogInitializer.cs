#pragma warning disable CA2255 // ModuleInitializer is the load hook for the composed streaming catalog.
using System.Runtime.CompilerServices;

namespace RedditPodcastPoster.StreamingCatalog;

internal static class StreamingCatalogInitializer
{
    [ModuleInitializer]
    internal static void Initialize() => StreamingCatalogLoader.EnsureLoaded();
}

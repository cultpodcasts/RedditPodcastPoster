namespace RedditPodcastPoster.OpenGraph.Extractors;

/// <summary>
/// URL path heuristics for streaming catalogue pages so recommended/carousel
/// JSON-LD <c>Movie</c> blobs cannot override a series path (and vice versa).
/// </summary>
public static class StreamingCataloguePathHints
{
    public static bool IsSeriesPath(Uri url) =>
        PathHasSegment(url, "shows", "series", "show", "tv-shows", "tv");

    public static bool IsMoviePath(Uri url) =>
        PathHasSegment(url, "movies", "movie");

    private static bool PathHasSegment(Uri url, params string[] segments)
    {
        var parts = url.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in parts)
        {
            foreach (var segment in segments)
            {
                if (part.Equals(segment, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
    }
}

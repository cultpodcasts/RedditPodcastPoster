namespace RedditPodcastPoster.Models.Podcasts;

/// <summary>
/// Discovery+ and HBO Max share Warner Bros. Discovery catalogue UUIDs in the path.
/// Submit rewrites one host to the sibling so a single paste stores both service links.
/// </summary>
public static class WbdCatalogueSiblingUrls
{
    private const string DiscoveryPlusHost = "www.discoveryplus.com";
    private const string MaxHost = "www.max.com";

    /// <summary>
    /// When <paramref name="service"/> is Discovery+ or HBO Max and the path carries a catalogue UUID,
    /// returns the sibling service and URL (query and fragment dropped). Otherwise false.
    /// </summary>
    public static bool TryGet(
        StreamingService service,
        Uri url,
        out StreamingService sibling,
        out Uri siblingUrl)
    {
        sibling = default;
        siblingUrl = null!;
        if (!url.IsAbsoluteUri)
        {
            return false;
        }

        var parts = url.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (!parts.Any(part => Guid.TryParse(part, out _)))
        {
            return false;
        }

        if (service == StreamingService.DiscoveryPlus)
        {
            var tail = CatalogueTail(parts, "show", "movie");
            if (tail.Length == 0)
            {
                return false;
            }

            sibling = StreamingService.HboMax;
            siblingUrl = Build(MaxHost, tail);
            return true;
        }

        if (service == StreamingService.HboMax)
        {
            var tail = CatalogueTail(parts, "show", "shows", "movie", "movies");
            if (tail.Length == 0)
            {
                return false;
            }

            sibling = StreamingService.DiscoveryPlus;
            siblingUrl = Build(DiscoveryPlusHost, NormalizeMaxKinds(tail));
            return true;
        }

        return false;
    }

    private static string[] CatalogueTail(string[] parts, params string[] kinds)
    {
        for (var i = 0; i < parts.Length; i++)
        {
            if (kinds.Any(kind => parts[i].Equals(kind, StringComparison.OrdinalIgnoreCase)))
            {
                return parts[i..];
            }
        }

        return [];
    }

    private static string[] NormalizeMaxKinds(string[] tail)
    {
        if (tail.Length == 0)
        {
            return tail;
        }

        var kind = tail[0];
        if (kind.Equals("shows", StringComparison.OrdinalIgnoreCase))
        {
            return ["show", ..tail[1..]];
        }

        if (kind.Equals("movies", StringComparison.OrdinalIgnoreCase))
        {
            return ["movie", ..tail[1..]];
        }

        return tail;
    }

    private static Uri Build(string host, string[] pathSegments) =>
        new($"https://{host}/{string.Join('/', pathSegments)}");
}

using RedditPodcastPoster.Models.Serialization;

namespace RedditPodcastPoster.Discovery.Services;

/// <summary>
/// Discover console CLI: normalise <c>-t</c> / <c>--since</c> as UTC and reject a future baseline (#1212).
/// Unspecified-kind means UTC — never <see cref="DateTime.ToUniversalTime"/>.
/// </summary>
public static class DiscoverCliSince
{
    /// <summary>
    /// Returns the same instant as UTC. Throws when the normalised value is after <paramref name="utcNow"/>.
    /// </summary>
    public static DateTime Resolve(DateTime since, DateTime? utcNow = null)
    {
        var normalised = UtcDateTimes.ToUtc(since);
        var now = utcNow ?? DateTime.UtcNow;
        if (normalised > UtcDateTimes.ToUtc(now))
        {
            throw new InvalidOperationException($"'{nameof(since)}' is in the future. ");
        }

        return normalised;
    }
}

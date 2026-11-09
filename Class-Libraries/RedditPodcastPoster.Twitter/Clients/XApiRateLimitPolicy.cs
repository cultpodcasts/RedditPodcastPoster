using RedditPodcastPoster.Models.Serialization;
using RedditPodcastPoster.Models.Social;

namespace RedditPodcastPoster.Twitter.Clients;

/// <summary>
/// Pure rules for when X must be skipped and how a LookUps <see cref="XApiRateLimitState"/> is updated from
/// response headers / problem types. Kept apart from HTTP and Cosmos so BusinessRules can assert without I/O.
/// </summary>
public static class XApiRateLimitPolicy
{
    public const string RateLimitReason = "rate-limit";
    public const string UsageCappedReason = "usage-capped";

    /// <summary>
    /// When usage is capped without a reset header, skip until the start of the next UTC calendar month
    /// (typical monthly credit window). A successful call clears the marker earlier if credits return.
    /// </summary>
    public static DateTime UsageCapSkipUntilUtc(DateTime nowUtc)
    {
        var utc = UtcDateTimes.ToUtc(nowUtc);
        return new DateTime(utc.Year, utc.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(1);
    }

    /// <summary>
    /// Parses <c>x-rate-limit-reset</c> (Unix seconds) from response headers. Returns null when missing or malformed.
    /// </summary>
    public static DateTime? TryParseResetUtc(IEnumerable<KeyValuePair<string, IEnumerable<string>>> headers)
    {
        foreach (var header in headers)
        {
            if (!header.Key.Equals("x-rate-limit-reset", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var value = header.Value.FirstOrDefault();
            if (long.TryParse(value, out var unixSeconds))
            {
                return DateTimeOffset.FromUnixTimeSeconds(unixSeconds).UtcDateTime;
            }
        }

        return null;
    }

    /// <summary>Active skip, if any. Usage-capped takes precedence over a concurrent rate-limit window.</summary>
    public static XApiSkipReason? GetActiveSkip(XApiRateLimitState? state, DateTime nowUtc)
    {
        if (state is null)
        {
            return null;
        }

        var now = UtcDateTimes.ToUtc(nowUtc);
        if (state.UsageCappedUntilUtc.HasValue && UtcDateTimes.ToUtc(state.UsageCappedUntilUtc.Value) > now)
        {
            return XApiSkipReason.UsageCapped;
        }

        if (state.RateLimitResetUtc.HasValue && UtcDateTimes.ToUtc(state.RateLimitResetUtc.Value) > now)
        {
            return XApiSkipReason.RateLimited;
        }

        return null;
    }

    public static void ApplyRateLimit(XApiRateLimitState state, DateTime? resetUtc, DateTime nowUtc)
    {
        var now = UtcDateTimes.ToUtc(nowUtc);
        // No usable reset header: skip the rest of this UTC hour so the next hourly Indexer run can retry.
        var until = resetUtc.HasValue && UtcDateTimes.ToUtc(resetUtc.Value) > now
            ? UtcDateTimes.ToUtc(resetUtc.Value)
            : new DateTime(now.Year, now.Month, now.Day, now.Hour, 0, 0, DateTimeKind.Utc).AddHours(1);

        if (!state.RateLimitResetUtc.HasValue || UtcDateTimes.ToUtc(state.RateLimitResetUtc.Value) < until)
        {
            state.RateLimitResetUtc = until;
        }

        state.LastReason = RateLimitReason;
        state.UpdatedUtc = now;
    }

    public static void ApplyUsageCap(XApiRateLimitState state, DateTime nowUtc)
    {
        var now = UtcDateTimes.ToUtc(nowUtc);
        var until = UsageCapSkipUntilUtc(now);
        if (!state.UsageCappedUntilUtc.HasValue || UtcDateTimes.ToUtc(state.UsageCappedUntilUtc.Value) < until)
        {
            state.UsageCappedUntilUtc = until;
        }

        state.LastReason = UsageCappedReason;
        state.UpdatedUtc = now;
    }

    /// <summary>Clears expired and all skip windows after a successful X call (credits / quota recovered).</summary>
    public static bool Clear(XApiRateLimitState state, DateTime nowUtc)
    {
        var had = state.RateLimitResetUtc.HasValue || state.UsageCappedUntilUtc.HasValue;
        state.RateLimitResetUtc = null;
        state.UsageCappedUntilUtc = null;
        state.LastReason = null;
        state.UpdatedUtc = UtcDateTimes.ToUtc(nowUtc);
        return had;
    }
}

public enum XApiSkipReason
{
    RateLimited = 1,
    UsageCapped
}

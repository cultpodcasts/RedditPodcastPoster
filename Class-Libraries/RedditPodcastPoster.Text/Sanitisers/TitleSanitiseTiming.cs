using System.Diagnostics;

namespace RedditPodcastPoster.Text.Sanitisers;

/// <summary>
/// Stopwatch tick contributions for one title sanitise.
/// When many titles run in parallel, sum ticks across titles (CPU work) vs wall <c>sanitise-ms</c>.
/// </summary>
public readonly record struct TitleSanitiseTiming(
    long PrepTicks,
    long RulesResolveTicks,
    long LowerCaseTicks,
    long UniversalKnownTermsTicks,
    long LanguageKnownTermsTicks,
    long PodcastKnownTermsTicks,
    long SubjectKnownTermsTicks,
    long FinishTicks,
    int UniversalKnownTermCount,
    int LanguageKnownTermCount,
    int LowerCaseTermCount)
{
    public long TotalTicks =>
        PrepTicks + RulesResolveTicks + LowerCaseTicks + UniversalKnownTermsTicks + LanguageKnownTermsTicks +
        PodcastKnownTermsTicks + SubjectKnownTermsTicks + FinishTicks;

    public static long TicksToMs(long ticks) =>
        (long)(ticks * 1000.0 / Stopwatch.Frequency);
}

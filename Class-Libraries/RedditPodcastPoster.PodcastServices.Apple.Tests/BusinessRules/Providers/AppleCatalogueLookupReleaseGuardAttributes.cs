using RedditPodcastPoster.Episodes.TestSupport.TimeZones;
using Xunit;

namespace RedditPodcastPoster.PodcastServices.Apple.Tests.BusinessRules.Providers;

/// <summary>Shared settings for <see cref="AppleCatalogueLookupReleaseEastOfUtcHostGuard"/> and the child run it starts.</summary>
internal static class AppleCatalogueLookupReleaseGuard
{
    /// <summary>Set to <c>1</c> by the guard in the child test host only.</summary>
    public const string ChildVariable = "APPLE_CATALOGUE_LOOKUP_RELEASE_GUARD";

    public static bool InChild => Environment.GetEnvironmentVariable(ChildVariable) == "1";
}

/// <summary>
/// A fact that runs only inside the guard's child test host, where it proves the child really is on
/// <see cref="EastOfUtcChildTestHost.EastOfUtcZone"/> time. Elsewhere it is skipped, because the host zone is not set.
/// </summary>
public sealed class AppleCatalogueLookupReleaseGuardChildFactAttribute : FactAttribute
{
    public AppleCatalogueLookupReleaseGuardChildFactAttribute()
    {
        if (!AppleCatalogueLookupReleaseGuard.InChild)
        {
            Skip = $"Runs only in the {nameof(AppleCatalogueLookupReleaseEastOfUtcHostGuard)} child test host " +
                   $"({AppleCatalogueLookupReleaseGuard.ChildVariable}=1, TZ={EastOfUtcChildTestHost.EastOfUtcZone}).";
        }
    }
}

/// <summary>
/// A fact that is skipped on Windows, where the TZ variable is ignored, so a child test host cannot be moved east of
/// UTC and the guard would prove nothing.
/// </summary>
public sealed class TzHonouringHostFactAttribute : FactAttribute
{
    public TzHonouringHostFactAttribute()
    {
        if (OperatingSystem.IsWindows())
        {
            Skip = "Windows ignores TZ, so the child test host cannot be put on Pacific/Auckland time; " +
                   "the guard runs on Linux and macOS (CI is Linux).";
        }
    }
}

using Indexer.Models;
using Indexer.Activities;

namespace Indexer.Orchestrations;

/// <summary>
/// Selects which indexer pass pair runs each hour so YouTube-enabled hours (every 6h) cover
/// both lower (1-2) and upper (3-4) podcast batches without increasing API quota.
/// </summary>
public static class HourlyIndexingPassSelector
{
    public static (int FirstPass, int LastPass) SelectPasses(int hourUtc, int totalPasses = 4)
    {
        if (totalPasses < 2 || totalPasses % 2 != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(totalPasses), "Total passes must be a positive even number.");
        }

        if (hourUtc is < 0 or > 23)
        {
            throw new ArgumentOutOfRangeException(nameof(hourUtc), "Hour must be between 0 and 23.");
        }

        var passesPerHour = totalPasses / 2;
        var useLowerBatches = UseLowerBatches(hourUtc);
        var firstPass = useLowerBatches ? 1 : passesPerHour + 1;
        return (firstPass, firstPass + passesPerHour - 1);
    }

    internal static bool UseLowerBatches(int hour)
    {
        if (hour % 6 == 0)
        {
            return hour % 12 == 0;
        }

        if (hour % 2 == 1)
        {
            var segmentStartHour = hour / 6 * 6;
            var youTubeHourUsedLowerBatches = segmentStartHour % 12 == 0;
            return !youTubeHourUsedLowerBatches;
        }

        return true;
    }
}

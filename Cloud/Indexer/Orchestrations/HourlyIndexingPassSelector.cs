using Indexer.Services;

namespace Indexer.Orchestrations;

/// <summary>
/// Selects which indexer pass pair runs each hour so YouTube-enabled hours (every 3h) cover
/// both lower (1-2) and upper (3-4) podcast batches. Even UTC hours run passes 1–2; odd hours
/// run 3–4 — so YouTube hours 0/6/12/18 hit the lower half and 3/9/15/21 hit the upper half.
/// Delegates to <see cref="HourlyIndexingSchedule"/> so pass pairing stays one source of truth.
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

        if (totalPasses != 4)
        {
            // Non-default totals keep the original even/odd half split without the full schedule.
            var passesPerHour = totalPasses / 2;
            var useLowerBatches = UseLowerBatches(hourUtc);
            var first = useLowerBatches ? 1 : passesPerHour + 1;
            return (first, first + passesPerHour - 1);
        }

        var plans = HourlyIndexingSchedule.For(new DateTime(2001, 1, 1, hourUtc, 0, 0, DateTimeKind.Utc));
        return (plans[0].Pass, plans[^1].Pass);
    }

    internal static bool UseLowerBatches(int hour) => hour % 2 == 0;
}

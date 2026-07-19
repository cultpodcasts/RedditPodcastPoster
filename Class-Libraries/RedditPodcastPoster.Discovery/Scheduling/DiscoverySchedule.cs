namespace RedditPodcastPoster.Discovery.Scheduling;

/// <summary>
/// A Discovery run slot identified by UK local date + HH:mm on the configured schedule.
/// </summary>
public readonly record struct DiscoverySlot(
    string SlotId,
    DateTimeOffset SlotStartUtc,
    DateTimeOffset SlotStartUk,
    TimeOnly RunTimeUk);

/// <summary>
/// UK-local Discovery schedule helpers. Timer cron is every 30 minutes; these methods decide
/// whether a tick matches a configured run time and compute slot identity / prior slot.
/// </summary>
public static class DiscoverySchedule
{
    public static readonly TimeSpan DefaultGrace = TimeSpan.FromMinutes(15);

    public static readonly IReadOnlyList<TimeOnly> DefaultRunTimesUk =
    [
        new TimeOnly(8, 0),
        new TimeOnly(22, 0)
    ];

    public static TimeZoneInfo ResolveUkTimeZone(string? configuredTimeZoneId = null)
    {
        var candidates = new List<string>();
        if (!string.IsNullOrWhiteSpace(configuredTimeZoneId))
        {
            candidates.Add(configuredTimeZoneId.Trim());
        }

        // Prefer Windows id on Azure Functions Windows hosts; fall back to IANA.
        candidates.Add("GMT Standard Time");
        candidates.Add("Europe/London");

        foreach (var timeZoneId in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            }
            catch (TimeZoneNotFoundException)
            {
            }
            catch (InvalidTimeZoneException)
            {
            }
        }

        throw new InvalidOperationException(
            "Unable to resolve UK time zone (tried GMT Standard Time / Europe/London).");
    }

    public static IReadOnlyList<TimeOnly> ParseRunTimes(IEnumerable<string>? runTimes)
    {
        if (runTimes is null)
        {
            return DefaultRunTimesUk;
        }

        var parsed = new List<TimeOnly>();
        foreach (var raw in runTimes)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                continue;
            }

            if (!TimeOnly.TryParse(raw.Trim(), out var time))
            {
                throw new FormatException($"Invalid Discovery schedule runTime '{raw}'. Expected HH:mm.");
            }

            if (time.Minute is not (0 or 30) || time.Second != 0 || time.Millisecond != 0)
            {
                throw new FormatException(
                    $"Discovery schedule runTime '{raw}' must be on a 30-minute grid (minutes 00 or 30).");
            }

            if (!parsed.Contains(time))
            {
                parsed.Add(time);
            }
        }

        if (parsed.Count == 0)
        {
            return DefaultRunTimesUk;
        }

        parsed.Sort();
        return parsed;
    }

    /// <summary>
    /// When the timer fires: match UK local time to a scheduled run within <paramref name="grace"/>.
    /// Returns null when this tick should no-op.
    /// </summary>
    public static DiscoverySlot? TryMatchDueSlot(
        DateTime utcNow,
        IReadOnlyList<TimeOnly> runTimesUk,
        TimeSpan? grace = null,
        TimeZoneInfo? ukTimeZone = null)
    {
        if (runTimesUk.Count == 0)
        {
            runTimesUk = DefaultRunTimesUk;
        }

        var tz = ukTimeZone ?? ResolveUkTimeZone();
        var graceSpan = grace ?? DefaultGrace;
        var ukNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utcNow, DateTimeKind.Utc), tz);

        DiscoverySlot? best = null;
        var bestDelta = TimeSpan.MaxValue;

        // Check today and yesterday so late wakes near midnight still match yesterday's last slot.
        for (var dayOffset = -1; dayOffset <= 0; dayOffset++)
        {
            var day = DateOnly.FromDateTime(ukNow).AddDays(dayOffset);
            foreach (var runTime in runTimesUk)
            {
                var slotUkLocal = day.ToDateTime(runTime);
                if (tz.IsInvalidTime(slotUkLocal))
                {
                    // Spring-forward gap: this local wall time does not exist — Dynamic recovery covers it.
                    continue;
                }

                var slotUk = new DateTimeOffset(slotUkLocal, tz.GetUtcOffset(slotUkLocal));
                var delta = (ukNow - slotUk.DateTime).Duration();
                if (delta > graceSpan)
                {
                    continue;
                }

                // Prefer the closest match; on equal delta prefer the earlier slot (deterministic).
                if (delta < bestDelta ||
                    (delta == bestDelta && best is not null && slotUk < best.Value.SlotStartUk))
                {
                    bestDelta = delta;
                    best = CreateSlot(day, runTime, tz);
                }
            }
        }

        return best;
    }

    /// <summary>
    /// Latest scheduled slot whose start is &lt;= <paramref name="utcNow"/> (no grace).
    /// Used for slot identity of running instances and stale-run detection.
    /// </summary>
    public static DiscoverySlot GetLatestDueSlot(
        DateTime utcNow,
        IReadOnlyList<TimeOnly> runTimesUk,
        TimeZoneInfo? ukTimeZone = null)
    {
        if (runTimesUk.Count == 0)
        {
            runTimesUk = DefaultRunTimesUk;
        }

        var tz = ukTimeZone ?? ResolveUkTimeZone();
        var ukNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utcNow, DateTimeKind.Utc), tz);

        DiscoverySlot? latest = null;
        for (var dayOffset = -2; dayOffset <= 0; dayOffset++)
        {
            var day = DateOnly.FromDateTime(ukNow).AddDays(dayOffset);
            foreach (var runTime in runTimesUk)
            {
                var slotUkLocal = day.ToDateTime(runTime);
                if (tz.IsInvalidTime(slotUkLocal))
                {
                    continue;
                }

                var slotUk = new DateTimeOffset(slotUkLocal, tz.GetUtcOffset(slotUkLocal));
                if (slotUk.UtcDateTime > utcNow)
                {
                    continue;
                }

                var candidate = CreateSlot(day, runTime, tz);
                if (latest is null || candidate.SlotStartUtc > latest.Value.SlotStartUtc)
                {
                    latest = candidate;
                }
            }
        }

        if (latest is null)
        {
            // Extremely early after schedule introduction: fall back to previous calendar day's last run.
            var day = DateOnly.FromDateTime(ukNow).AddDays(-1);
            var runTime = runTimesUk[^1];
            return CreateSlot(day, runTime, tz);
        }

        return latest.Value;
    }

    public static DiscoverySlot GetPriorSlot(
        DiscoverySlot current,
        IReadOnlyList<TimeOnly> runTimesUk,
        TimeZoneInfo? ukTimeZone = null)
    {
        if (runTimesUk.Count == 0)
        {
            runTimesUk = DefaultRunTimesUk;
        }

        var tz = ukTimeZone ?? ResolveUkTimeZone();
        var sorted = runTimesUk.OrderBy(t => t).ToList();
        var index = sorted.FindIndex(t => t == current.RunTimeUk);
        if (index < 0)
        {
            throw new InvalidOperationException(
                $"Current slot run time {current.RunTimeUk:HH\\:mm} is not in the schedule.");
        }

        DateOnly day;
        TimeOnly priorTime;
        if (index == 0)
        {
            priorTime = sorted[^1];
            day = DateOnly.FromDateTime(current.SlotStartUk.DateTime).AddDays(-1);
        }
        else
        {
            priorTime = sorted[index - 1];
            day = DateOnly.FromDateTime(current.SlotStartUk.DateTime);
        }

        return CreateSlot(day, priorTime, tz);
    }

    /// <summary>
    /// A run is stale when the latest due slot for <paramref name="currentUtcDateTime"/> differs
    /// from the slot that was scheduled (<paramref name="scheduledSlotStartUtc"/>).
    /// </summary>
    public static bool IsStaleRun(
        DateTimeOffset scheduledSlotStartUtc,
        DateTime currentUtcDateTime,
        IReadOnlyList<TimeOnly> runTimesUk,
        TimeZoneInfo? ukTimeZone = null)
    {
        var currentSlot = GetLatestDueSlot(currentUtcDateTime, runTimesUk, ukTimeZone);
        return currentSlot.SlotStartUtc != scheduledSlotStartUtc;
    }

    public static IReadOnlyList<DiscoverySlot> PreviewNextRuns(
        DateTime utcNow,
        IReadOnlyList<TimeOnly> runTimesUk,
        int count,
        TimeZoneInfo? ukTimeZone = null)
    {
        if (count <= 0)
        {
            return [];
        }

        if (runTimesUk.Count == 0)
        {
            runTimesUk = DefaultRunTimesUk;
        }

        var tz = ukTimeZone ?? ResolveUkTimeZone();
        var sorted = runTimesUk.OrderBy(t => t).ToList();
        var ukNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utcNow, DateTimeKind.Utc), tz);
        var results = new List<DiscoverySlot>(count);

        for (var dayOffset = 0; results.Count < count && dayOffset < 14; dayOffset++)
        {
            var day = DateOnly.FromDateTime(ukNow).AddDays(dayOffset);
            foreach (var runTime in sorted)
            {
                var slotUkLocal = day.ToDateTime(runTime);
                if (tz.IsInvalidTime(slotUkLocal))
                {
                    continue;
                }

                var slot = CreateSlot(day, runTime, tz);
                if (slot.SlotStartUtc.UtcDateTime <= utcNow)
                {
                    continue;
                }

                results.Add(slot);
                if (results.Count >= count)
                {
                    break;
                }
            }
        }

        return results;
    }

    public static string FormatSlot(DiscoverySlot slot) =>
        $"{slot.SlotId} ({slot.SlotStartUtc.UtcDateTime:yyyy-MM-dd HH:mm} UTC)";

    private static DiscoverySlot CreateSlot(DateOnly dayUk, TimeOnly runTimeUk, TimeZoneInfo tz)
    {
        var local = dayUk.ToDateTime(runTimeUk);
        // Ambiguous (fall-back): pick the first occurrence for stable slot identity.
        var offset = tz.IsAmbiguousTime(local)
            ? tz.GetAmbiguousTimeOffsets(local)[0]
            : tz.GetUtcOffset(local);
        var slotUk = new DateTimeOffset(local, offset);
        var slotId = $"{dayUk:yyyy-MM-dd} {runTimeUk:HH\\:mm} UK";
        return new DiscoverySlot(slotId, slotUk.ToUniversalTime(), slotUk, runTimeUk);
    }
}

namespace Discovery;

public static class DiscoverySchedule
{
    public static readonly int[] ScheduledHoursUtc = [2, 8, 14, 20];
    public const int ScheduledMinuteUtc = 33;

    public static DateTimeOffset GetSlotStartForTime(DateTime utcNow)
    {
        var candidates = new List<DateTimeOffset>();
        for (var dayOffset = -1; dayOffset <= 0; dayOffset++)
        {
            var day = utcNow.Date.AddDays(dayOffset);
            foreach (var hour in ScheduledHoursUtc)
            {
                candidates.Add(new DateTimeOffset(day.AddHours(hour).AddMinutes(ScheduledMinuteUtc), TimeSpan.Zero));
            }
        }

        return candidates
            .Where(slotStart => slotStart.UtcDateTime <= utcNow)
            .MaxBy(slotStart => slotStart);
    }

    public static DateTimeOffset GetPriorSlotStart(DateTimeOffset currentSlotStart) =>
        currentSlotStart.AddHours(-6);
}

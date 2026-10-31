using Api.Dtos;
using RedditPodcastPoster.Models.Discovery;
using DiscoveryScheduleLogic = RedditPodcastPoster.Discovery.Scheduling.DiscoverySchedule;

namespace Api.Dtos.Mapping;

public static class DiscoveryScheduleResponseBuilder
{
    /// <param name="utcNow">The current UTC time; the preview lists the next runs after it.</param>
    public static DiscoveryScheduleResponse Build(
        DiscoveryScheduleConfig config,
        bool isDefault,
        int nextRunsPreviewCount,
        DateTime utcNow)
    {
        var runTimes = DiscoveryScheduleLogic.ParseRunTimes(config.RunTimes);
        var tz = DiscoveryScheduleLogic.ResolveUkTimeZone(config.TimeZoneId);
        var next = DiscoveryScheduleLogic.PreviewNextRuns(utcNow, runTimes, nextRunsPreviewCount, tz);
        return new DiscoveryScheduleResponse
        {
            RunTimes = runTimes.Select(t => t.ToString("HH\\:mm")).ToList(),
            TimeZoneId = config.TimeZoneId,
            Enabled = config.Enabled,
            IsDefault = isDefault,
            NextRuns = next.Select(s => new DiscoveryScheduleResponse.NextRun
            {
                SlotId = s.SlotId,
                SlotStartUtc = s.SlotStartUtc,
                SlotStartUk = s.SlotStartUk
            }).ToList()
        };
    }
}

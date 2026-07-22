using Api.Dtos;
using RedditPodcastPoster.Models.Discovery;
using DiscoveryScheduleLogic = RedditPodcastPoster.Discovery.Scheduling.DiscoverySchedule;

namespace Api.Services.DiscoverySchedule;

internal static class DiscoveryScheduleResponseBuilder
{
    public static DiscoveryScheduleResponse Build(
        DiscoveryScheduleConfig config,
        bool isDefault,
        int nextRunsPreviewCount)
    {
        var runTimes = DiscoveryScheduleLogic.ParseRunTimes(config.RunTimes);
        var tz = DiscoveryScheduleLogic.ResolveUkTimeZone(config.TimeZoneId);
        var next = DiscoveryScheduleLogic.PreviewNextRuns(DateTime.UtcNow, runTimes, nextRunsPreviewCount, tz);
        return new DiscoveryScheduleResponse
        {
            RunTimes = runTimes.Select(t => t.ToString("HH\\:mm")).ToList(),
            TimeZoneId = config.TimeZoneId,
            Enabled = config.Enabled,
            IsDefault = isDefault,
            NextRuns = next.Select(s => new DiscoveryScheduleNextRun
            {
                SlotId = s.SlotId,
                SlotStartUtc = s.SlotStartUtc,
                SlotStartUk = s.SlotStartUk
            }).ToList()
        };
    }
}

using System.Net;
using Api.Dtos;
using Api.Extensions;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Auth0;
using RedditPodcastPoster.Discovery.Scheduling;
using RedditPodcastPoster.Models.Discovery;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;

namespace Api.Handlers;

public interface IDiscoveryScheduleHandler
{
    Task<HttpResponseData> Get(HttpRequestData req, ClientPrincipal? clientPrincipal, CancellationToken c);
    Task<HttpResponseData> Put(HttpRequestData req, DiscoveryScheduleUpdateRequest body, ClientPrincipal? clientPrincipal,
        CancellationToken c);
}

public class DiscoveryScheduleHandler(
    ILookupRepository lookupRepository,
    ILogger<DiscoveryScheduleHandler> logger) : IDiscoveryScheduleHandler
{
    private const int NextRunsPreviewCount = 6;

    public async Task<HttpResponseData> Get(HttpRequestData r, ClientPrincipal? _, CancellationToken c)
    {
        try
        {
            var persisted = await lookupRepository.GetDiscoveryScheduleConfig();
            var config = persisted ?? DiscoveryScheduleConfig.CreateDefault();
            var response = BuildResponse(config, isDefault: persisted is null);
            return await r.CreateResponse(HttpStatusCode.OK).WithJsonBody(response, c);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failure to get DiscoveryScheduleConfig.");
            return r.CreateResponse(HttpStatusCode.InternalServerError);
        }
    }

    public async Task<HttpResponseData> Put(HttpRequestData r, DiscoveryScheduleUpdateRequest body,
        ClientPrincipal? _, CancellationToken c)
    {
        try
        {
            if (body.RunTimes is null || body.RunTimes.Count == 0)
            {
                return await r.CreateResponse(HttpStatusCode.BadRequest)
                    .WithJsonBody(new { error = "runTimes must contain at least one HH:mm value on a 30-minute grid." }, c);
            }

            IReadOnlyList<TimeOnly> parsed;
            try
            {
                parsed = DiscoverySchedule.ParseRunTimes(body.RunTimes);
            }
            catch (FormatException ex)
            {
                return await r.CreateResponse(HttpStatusCode.BadRequest)
                    .WithJsonBody(new { error = ex.Message }, c);
            }

            var config = await lookupRepository.GetDiscoveryScheduleConfig() ?? new DiscoveryScheduleConfig();
            config.RunTimes = parsed.Select(t => t.ToString("HH\\:mm")).ToList();
            if (!string.IsNullOrWhiteSpace(body.TimeZoneId))
            {
                config.TimeZoneId = body.TimeZoneId.Trim();
            }

            if (body.Enabled is { } enabled)
            {
                config.Enabled = enabled;
            }

            // Validate timezone resolves on this host.
            DiscoverySchedule.ResolveUkTimeZone(config.TimeZoneId);

            await lookupRepository.SaveDiscoveryScheduleConfig(config);
            var response = BuildResponse(config, isDefault: false);
            return await r.CreateResponse(HttpStatusCode.OK).WithJsonBody(response, c);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failure to update DiscoveryScheduleConfig.");
            return r.CreateResponse(HttpStatusCode.InternalServerError);
        }
    }

    private static DiscoveryScheduleResponse BuildResponse(DiscoveryScheduleConfig config, bool isDefault)
    {
        var runTimes = DiscoverySchedule.ParseRunTimes(config.RunTimes);
        var tz = DiscoverySchedule.ResolveUkTimeZone(config.TimeZoneId);
        var next = DiscoverySchedule.PreviewNextRuns(DateTime.UtcNow, runTimes, NextRunsPreviewCount, tz);
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

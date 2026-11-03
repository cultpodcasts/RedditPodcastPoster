using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Api.Configuration;
using Api;
using Api.Dtos.Extensions;
using Api.Factories;
using Api.Handlers.Episodes;
using Api.Handlers.News;
using Api.Models;
using Azure.Diagnostics;
using RedditPodcastPoster.Models.ContentKinds;

namespace Api.Controllers;

public class NewsReportController(
    IGetNewsReportHandler getNewsReportHandler,
    IPostNewsReportHandler postNewsReportHandler,
    IPublishEpisodeHandler publishEpisodeHandler,
    IClientPrincipalFactory clientPrincipalFactory,
    ILogger<NewsReportController> logger,
    IOptions<HostingOptions> hostingOptions,
    IMemoryProbeOrchestrator memoryProbeOrchestrator)
    : MemoryProbedHttpBaseClass(clientPrincipalFactory, hostingOptions, memoryProbeOrchestrator, logger)
{
    [Function("NewsReportGet")]
    public Task<HttpResponseData> Get(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "newsreport/{reportId:guid}")]
        HttpRequestData req,
        Guid reportId,
        CancellationToken ct) =>
        HandleRequest(req, ["curate"], reportId, getNewsReportHandler.Handle, Unauthorised, ct);

    [Function("NewsReportPatch")]
    public Task<HttpResponseData> Patch(
        [HttpTrigger(AuthorizationLevel.Anonymous, "patch", Route = "newsreport/{reportId:guid}")]
        HttpRequestData req,
        Guid reportId,
        [FromBody] Dtos.NewsReportChangeRequest change,
        CancellationToken ct) =>
        HandleRequest(
            req,
            ["curate"],
            new NewsReportChangeRequestWrapper(reportId, change.ToModel()),
            postNewsReportHandler.Handle,
            Unauthorised,
            ct);

    [Function("NewsReportPublish")]
    public Task<HttpResponseData> Publish(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "newsreport/{reportId:guid}/publish")]
        HttpRequestData req,
        Guid reportId,
        [FromBody] Dtos.EpisodePublishRequest episodePostRequest,
        CancellationToken ct) =>
        HandleRequest(
            req,
            ["curate"],
            new EpisodePublishRequestWrapper(
                null, reportId, episodePostRequest.ToModel(), ContentKind.NewsReport),
            publishEpisodeHandler.Handle,
            Unauthorised,
            ct);
}

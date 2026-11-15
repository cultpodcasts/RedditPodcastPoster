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
    IDeleteNewsReportHandler deleteNewsReportHandler,
    IPublishEpisodeHandler publishEpisodeHandler,
    IPostNewsReportTvShowMoveHandler postNewsReportTvShowMoveHandler,
    IPostNewsReportFilmMoveHandler postNewsReportFilmMoveHandler,
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

    [Function("NewsReportDelete")]
    public Task<HttpResponseData> Delete(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "newsreport/{reportId:guid}")]
        HttpRequestData req,
        Guid reportId,
        CancellationToken ct) =>
        HandleRequest(req, ["admin"], reportId, deleteNewsReportHandler.Handle, Unauthorised, ct);

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

    /// <summary>
    /// Moves one news report into a TV show as a TV episode (same id, #1368). Body as Move to TV show for an episode:
    /// <c>tvShowId</c> or <c>tvShowName</c>, optional <c>createShow</c> + <c>publisher</c>, optional <c>title</c>.
    /// The news organisation is kept, even when empty (docs/move-news-report.md).
    /// </summary>
    [Function("NewsReportMoveToTvShow")]
    public Task<HttpResponseData> MoveToTvShow(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "newsreport/{reportId:guid}/tvshow")]
        HttpRequestData req,
        Guid reportId,
        [FromBody] Dtos.EpisodeTvShowMoveRequest? request,
        CancellationToken ct) =>
        HandleRequest(
            req,
            ["curate"],
            new NewsReportTvShowMoveCommand(reportId, request.ToModel()),
            postNewsReportTvShowMoveHandler.Handle,
            Unauthorised,
            ct);

    /// <summary>
    /// Moves one news report into a new film (same id, #1368). Body as Move to film for an episode: optional
    /// <c>title</c>, and <c>identityDecision</c> + <c>selectedIdentityKey</c> after an ambiguous-identity prompt.
    /// </summary>
    [Function("NewsReportMoveToFilm")]
    public Task<HttpResponseData> MoveToFilm(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "newsreport/{reportId:guid}/film")]
        HttpRequestData req,
        Guid reportId,
        [FromBody] Dtos.EpisodeFilmMoveRequest? request,
        CancellationToken ct) =>
        HandleRequest(
            req,
            ["curate"],
            new NewsReportFilmMoveCommand(reportId, request.ToModel()),
            postNewsReportFilmMoveHandler.Handle,
            Unauthorised,
            ct);
}

using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Api.Configuration;
using Api.Dtos.Extensions;
using Api.Factories;
using Api.Handlers.DiscoverySubmissions;
using Api.Models;
using Api.Services.Discovery;
using Azure.Diagnostics;

namespace Api.Controllers;

/// <summary>Supervisor review of discovery submits held for supervised curators.</summary>
public class DiscoverySubmissionsController(
    IGetDiscoverySubmissionsHandler getDiscoverySubmissionsHandler,
    IGetDiscoverySubmissionHandler getDiscoverySubmissionHandler,
    IPutDiscoverySubmissionReviewHandler putDiscoverySubmissionReviewHandler,
    IPostDiscoverySubmissionApplyHandler postDiscoverySubmissionApplyHandler,
    IPostDiscoverySubmissionRetryHandler postDiscoverySubmissionRetryHandler,
    IPostDiscoverySubmissionReleaseHandler postDiscoverySubmissionReleaseHandler,
    IPostDiscoveryReportReleaseHandler postDiscoveryReportReleaseHandler,
    IClientPrincipalFactory clientPrincipalFactory,
    ILogger<DiscoverySubmissionsController> logger,
    IOptions<HostingOptions> hostingOptions,
    IMemoryProbeOrchestrator memoryProbeOrchestrator)
    : MemoryProbedHttpBaseClass(clientPrincipalFactory, hostingOptions, memoryProbeOrchestrator, logger)
{
    private const string Route = "DiscoverySubmissions";
    private const string ItemRoute = Route + "/{submissionId:guid}";

    /// <summary>Viewers: supervisors/admins (queue) and supervised curators (their own submissions).</summary>
    private static readonly string[] ViewRoles =
        [DiscoveryPermissions.Supervise, DiscoveryPermissions.Admin, DiscoveryPermissions.CurateSupervised];

    private static readonly string[] SuperviseRoles = [DiscoveryPermissions.Supervise, DiscoveryPermissions.Admin];

    [Function("DiscoverySubmissionsGet")]
    public Task<HttpResponseData> List(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = Route)]
        HttpRequestData req,
        FunctionContext _,
        CancellationToken ct) =>
        HandleRequest(req, ViewRoles, getDiscoverySubmissionsHandler.Handle, Unauthorised, ct);

    [Function("DiscoverySubmissionGet")]
    public Task<HttpResponseData> Get(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = ItemRoute)]
        HttpRequestData req,
        Guid submissionId,
        FunctionContext _,
        CancellationToken ct) =>
        HandleRequest(req, ViewRoles, submissionId, getDiscoverySubmissionHandler.Handle, Unauthorised, ct);

    [Function("DiscoverySubmissionReviewPut")]
    public Task<HttpResponseData> PutReview(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = ItemRoute + "/review")]
        HttpRequestData req,
        Guid submissionId,
        FunctionContext _,
        [FromBody] Dtos.DiscoverySubmissionReviewRequestDto request,
        CancellationToken ct) =>
        HandleRequest(
            req,
            SuperviseRoles,
            new DiscoverySubmissionReviewRoute<DiscoverySubmissionReviewRequest>(submissionId, request.ToModel()),
            putDiscoverySubmissionReviewHandler.Handle,
            Unauthorised,
            ct);

    [Function("DiscoverySubmissionApplyPost")]
    public Task<HttpResponseData> PostApply(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = ItemRoute + "/apply")]
        HttpRequestData req,
        Guid submissionId,
        FunctionContext _,
        [FromBody] Dtos.DiscoverySubmissionApplyRequestDto request,
        CancellationToken ct) =>
        HandleRequest(
            req,
            SuperviseRoles,
            new DiscoverySubmissionReviewRoute<DiscoverySubmissionApplyRequest>(submissionId, request.ToModel()),
            postDiscoverySubmissionApplyHandler.Handle,
            Unauthorised,
            ct);

    [Function("DiscoverySubmissionRetryPost")]
    public Task<HttpResponseData> PostRetry(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = ItemRoute + "/retry")]
        HttpRequestData req,
        Guid submissionId,
        FunctionContext _,
        [FromBody] Dtos.DiscoverySubmissionRetryRequestDto request,
        CancellationToken ct) =>
        HandleRequest(
            req,
            SuperviseRoles,
            new DiscoverySubmissionReviewRoute<DiscoverySubmissionRetryRequest>(submissionId, request.ToModel()),
            postDiscoverySubmissionRetryHandler.Handle,
            Unauthorised,
            ct);

    [Function("DiscoverySubmissionReleasePost")]
    public Task<HttpResponseData> PostRelease(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = ItemRoute + "/release")]
        HttpRequestData req,
        Guid submissionId,
        FunctionContext _,
        [FromBody] Dtos.DiscoverySubmissionReleaseRequestDto request,
        CancellationToken ct) =>
        HandleRequest(
            req,
            SuperviseRoles,
            new DiscoverySubmissionReviewRoute<DiscoverySubmissionReleaseRequest>(submissionId, request.ToModel()),
            postDiscoverySubmissionReleaseHandler.Handle,
            Unauthorised,
            ct);

    /// <summary>Admin only: release a report stuck in PendingSupervisor with no live submission.</summary>
    [Function("DiscoveryReportReleasePost")]
    public Task<HttpResponseData> PostReportRelease(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "DiscoveryReports/{documentId:guid}/release")]
        HttpRequestData req,
        Guid documentId,
        FunctionContext _,
        [FromBody] Dtos.DiscoveryReportReleaseRequestDto request,
        CancellationToken ct) =>
        HandleRequest(
            req,
            [DiscoveryPermissions.Admin],
            new DiscoverySubmissionReviewRoute<DiscoveryReportReleaseRequest>(documentId, request.ToModel()),
            postDiscoveryReportReleaseHandler.Handle,
            Unauthorised,
            ct);
}

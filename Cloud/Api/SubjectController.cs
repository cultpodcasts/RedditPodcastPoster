using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Api.Configuration;
using Api.Dtos;
using Api.Models;
using Api.Factories;
using Api.Handlers;
using Api.Handlers.Discovery;
using Api.Handlers.DiscoverySchedule;
using Api.Handlers.Episodes;
using Api.Handlers.Homepage;
using Api.Handlers.People;
using Api.Handlers.Podcasts;
using Api.Handlers.Public;
using Api.Handlers.PushSubscriptions;
using Api.Handlers.SearchIndex;
using Api.Handlers.Subjects;
using Api.Handlers.SubmitUrl;
using Api.Handlers.Terms;
using Azure.Diagnostics;

namespace Api;

public class SubjectController(
    IGetSubjectHandler getSubjectHandler,
    IPostSubjectHandler postSubjectHandler,
    IPutSubjectHandler putSubjectHandler,
    IClientPrincipalFactory clientPrincipalFactory,
    ILogger<SubjectController> logger,
    IOptions<HostingOptions> hostingOptions,
    IMemoryProbeOrchestrator memoryProbeOrchestrator)
    : MemoryProbedHttpBaseClass(clientPrincipalFactory, hostingOptions, memoryProbeOrchestrator, logger)
{
    [Function("SubjectGet")]
    public Task<HttpResponseData> Get(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "subject/{subjectName}")]
        HttpRequestData req,
        string subjectName,
        FunctionContext executionContext,
        CancellationToken ct
    ) =>
        HandleRequest(
            req,
            ["curate"],
            subjectName,
            getSubjectHandler.Handle,
            Unauthorised,
            ct);

    [Function("SubjectPost")]
    public Task<HttpResponseData> Post(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "subject/{subjectId:guid}")]
        HttpRequestData req,
        Guid subjectId,
        FunctionContext executionContext,
        [FromBody] Subject subjectChangeRequest,
        CancellationToken ct
    ) =>
        HandleRequest(
            req,
            ["curate"],
            new SubjectChangeRequestWrapper(subjectId, subjectChangeRequest),
            postSubjectHandler.Handle,
            Unauthorised,
            ct);

    [Function("SubjectPut")]
    public Task<HttpResponseData> Put(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "subject")]
        HttpRequestData req,
        FunctionContext executionContext,
        [FromBody] Subject subjectChangeRequest,
        CancellationToken ct
    ) =>
        HandleRequest(
            req,
            ["curate"],
            subjectChangeRequest,
            putSubjectHandler.Handle,
            Unauthorised,
            ct);
}

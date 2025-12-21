using Api.Configuration;
using Api.Dtos;
using Api.Handlers;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Api;

public class SubjectController(
    ISubjectHandler subjectHandler,
    IClientPrincipalFactory clientPrincipalFactory,
    ILogger<SubjectController> logger,
    IOptions<HostingOptions> hostingOptions
) : BaseHttpFunction(clientPrincipalFactory, hostingOptions, logger)
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
            subjectHandler.Get,
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
            subjectHandler.Post,
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
            subjectHandler.Put, 
            Unauthorised, 
            ct);
}
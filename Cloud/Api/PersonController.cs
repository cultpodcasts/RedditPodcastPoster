using Api.Configuration;
using Api.Dtos;
using Api.Factories;
using Api.Handlers;
using Azure.Diagnostics;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Api;

public class PersonController(
    IPersonHandler personHandler,
    IClientPrincipalFactory clientPrincipalFactory,
    ILogger<PersonController> logger,
    IOptions<HostingOptions> hostingOptions,
    IMemoryProbeOrchestrator memoryProbeOrchestrator)
    : MemoryProbedHttpBaseClass(clientPrincipalFactory, hostingOptions, memoryProbeOrchestrator, logger)
{
    [Function("PeopleGet")]
    public Task<HttpResponseData> GetAll(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "people")]
        HttpRequestData req,
        FunctionContext executionContext,
        CancellationToken ct) =>
        HandleRequest(req, ["curate"], personHandler.GetAll, Unauthorised, ct);

    [Function("PersonGet")]
    public Task<HttpResponseData> Get(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "person/{personName}")]
        HttpRequestData req,
        string personName,
        FunctionContext executionContext,
        CancellationToken ct) =>
        HandleRequest(req, ["curate"], personName, personHandler.Get, Unauthorised, ct);

    [Function("PersonPost")]
    public Task<HttpResponseData> Post(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "person/{personId:guid}")]
        HttpRequestData req,
        Guid personId,
        FunctionContext executionContext,
        [FromBody] Person personChangeRequest,
        CancellationToken ct) =>
        HandleRequest(
            req,
            ["curate"],
            new PersonChangeRequestWrapper(personId, personChangeRequest),
            personHandler.Post,
            Unauthorised,
            ct);

    [Function("PersonPut")]
    public Task<HttpResponseData> Put(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "person")]
        HttpRequestData req,
        FunctionContext executionContext,
        [FromBody] Person personChangeRequest,
        CancellationToken ct) =>
        HandleRequest(req, ["curate"], personChangeRequest, personHandler.Put, Unauthorised, ct);
}

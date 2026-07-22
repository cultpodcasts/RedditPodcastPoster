using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Api.Configuration;
using Api.Dtos;
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
using Api.Models;
using Azure.Diagnostics;

namespace Api;

public class PersonController(
    IGetAllPeopleHandler getAllPeopleHandler,
    IGetPersonHandler getPersonHandler,
    IPostPersonHandler postPersonHandler,
    IPutPersonHandler putPersonHandler,
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
        HandleRequest(req, ["curate"], getAllPeopleHandler.Handle, Unauthorised, ct);

    [Function("PersonGet")]
    public Task<HttpResponseData> Get(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "person/{personName}")]
        HttpRequestData req,
        string personName,
        FunctionContext executionContext,
        CancellationToken ct) =>
        HandleRequest(req, ["curate"], personName, getPersonHandler.Handle, Unauthorised, ct);

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
            postPersonHandler.Handle,
            Unauthorised,
            ct);

    [Function("PersonPut")]
    public Task<HttpResponseData> Put(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "person")]
        HttpRequestData req,
        FunctionContext executionContext,
        [FromBody] Person personChangeRequest,
        CancellationToken ct) =>
        HandleRequest(req, ["curate"], personChangeRequest, putPersonHandler.Handle, Unauthorised, ct);
}

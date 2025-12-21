using Api.Configuration;
using Api.Dtos;
using Api.Factories;
using Api.Handlers;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Api;

public class DiscoveryCurationController(
    IDiscoveryCurationHandler discoveryCurationHandler,
    IClientPrincipalFactory clientPrincipalFactory,
    ILogger<DiscoveryCurationController> logger,
    IOptions<HostingOptions> hostingOptions)
    : BaseHttpFunction(clientPrincipalFactory, hostingOptions, logger)
{
    private const string? Route = "DiscoveryCuration";

    [Function("DiscoveryCurationGet")]
    public Task<HttpResponseData> Get(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = Route)]
        HttpRequestData req,
        FunctionContext _,
        CancellationToken ct) =>
        HandleRequest(
            req, 
            ["curate"], 
            discoveryCurationHandler.Get, 
            Unauthorised, 
            ct);

    [Function("DiscoveryCurationPost")]
    public Task<HttpResponseData> Post(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = Route)]
        HttpRequestData req,
        FunctionContext _,
        [FromBody] DiscoverySubmitRequest discoverySubmitRequest,
        CancellationToken ct) =>
        HandleRequest(
            req, 
            ["curate"], 
            discoverySubmitRequest, 
            discoveryCurationHandler.Post, 
            Unauthorised, 
            ct);
}
using Api.Configuration;
using Api.Handlers;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Api;

public class PublicController(
    IPublicHandler publicHandler,
    ILogger<EpisodeController> logger,
    IClientPrincipalFactory clientPrincipalFactory,
    IOptions<HostingOptions> hostingOptions)
    : BaseHttpFunction(clientPrincipalFactory, hostingOptions, logger)
{
    [Function("PublicEpisodeGet")]
    public Task<HttpResponseData> Get(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "public/episode/{episodeId:guid}")]
        HttpRequestData req,
        Guid episodeId,
        FunctionContext executionContext,
        CancellationToken ct
    ) =>
        HandlePublicRequest(
            req, 
            episodeId, 
            publicHandler.Get, 
            Unauthorised,
            ct);
}
using Api.Configuration;
using Api.Factories;
using Api.Handlers;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Api;

public class PublishController(
    IPublishHandler publishHandler,
    IClientPrincipalFactory clientPrincipalFactory,
    ILogger<PublishController> logger,
    IOptions<HostingOptions> hostingOptions)
    : BaseHttpFunction(clientPrincipalFactory, hostingOptions, logger)
{
    [Function("PublishHomepage")]
    public Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "publish/homepage")]
        HttpRequestData req,
        FunctionContext executionContext,
        CancellationToken ct
    ) =>
        HandleRequest(
            req, 
            ["admin"], 
            publishHandler.PublishHomepage, 
            Unauthorised,
            ct);
}
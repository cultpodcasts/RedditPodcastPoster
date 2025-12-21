using Api.Configuration;
using Api.Factories;
using Api.Handlers;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PushSubscription = Api.Dtos.PushSubscription;

namespace Api;

public class PushSubscriptionController(
    IPushSubscriptionHandler pushSubscriptionHandler,
    IClientPrincipalFactory clientPrincipalFactory,
    ILogger<PushSubscriptionController> logger,
    IOptions<HostingOptions> hostingOptions
) : BaseHttpFunction(clientPrincipalFactory, hostingOptions, logger)
{
    [Function("PushSubscription")]
    public Task<HttpResponseData> CreatePushSubscription(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "pushsubscription")]
        HttpRequestData req,
        [FromBody] PushSubscription pushSubscription,
        FunctionContext executionContext,
        CancellationToken ct
    ) =>
        HandleRequest(
            req, 
            ["admin"], 
            pushSubscription, 
            pushSubscriptionHandler.CreatePushSubscription,
            Unauthorised, 
            ct);
}
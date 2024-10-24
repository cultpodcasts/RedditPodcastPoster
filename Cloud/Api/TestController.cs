using System.Net;
using Api.Configuration;
using Api.Extensions;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Api;

public class TestController(
    ILogger<TestController> logger,
    ILogger<BaseHttpFunction> baseLogger,
    IOptions<HostingOptions> hostingOptions)
    : BaseHttpFunction(hostingOptions, baseLogger)
{
    [Function("Test")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")]
        HttpRequestData req,
        FunctionContext executionContext,
        CancellationToken ct)
    {
        logger.LogInformation($"{nameof(Run)} initiated.");

        return await HandleRequest(
            req,
            ["*"],
            async (r, cp, c) =>
            {
                var success = await req.CreateResponse(HttpStatusCode.OK)
                    .WithJsonBody(new {message = "success", cp = cp}, c);
                return success;
            },
            async (r, cp, c) =>
            {
                var failure = await req.CreateResponse(HttpStatusCode.Forbidden)
                    .WithJsonBody(new {message = "failure"}, c);
                return failure;
            },
            ct);
    }
}
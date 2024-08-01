using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Api;

public class Test(
    ILogger<Test> logger,
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
            async (r, c) =>
            {
                var success = await req.CreateResponse(HttpStatusCode.OK)
                    .WithJsonBody(new {message = "success"}, c);
                return success;
            },
            async (r, c) =>
            {
                var failure = await req.CreateResponse(HttpStatusCode.Forbidden)
                    .WithJsonBody(new {message = "failure"}, c);
                return failure;
            },
            ct);
    }
}
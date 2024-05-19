using System.Net;
using Api.Dtos;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Api;

public class Test(
    ILogger<Test> logger,
    IOptions<HostingOptions> hostingOptions)
    : BaseHttpFunction(hostingOptions)
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
                    .WithJsonBody(SubmitUrlResponse.Successful("Has principle."), c);
                return success;
            },
            async (r, c) =>
            {
                var failure = await req.CreateResponse(HttpStatusCode.Forbidden)
                    .WithJsonBody(SubmitUrlResponse.Failure("No principle."), c);
                return failure;
            },
            ct);
    }
}
using System.Net;
using Api.Dtos;
using Api.Extensions;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace Api;

public class Test(ILogger<Test> logger)
{
    [Function("Test")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")]
        HttpRequestData req,
        FunctionContext executionContext)
    {
        logger.LogInformation($"{nameof(Run)} initiated.");
        if (req.HasScope("submit"))
        {
            var success = req.CreateResponse(HttpStatusCode.OK);
            await success.WriteAsJsonAsync(SubmitUrlResponse.Successful("Has principle."));
            return success;
        }

        var failure = req.CreateResponse(HttpStatusCode.Forbidden);
        await failure.WriteAsJsonAsync(SubmitUrlResponse.Failure("No principle."));
        return failure;
    }
}
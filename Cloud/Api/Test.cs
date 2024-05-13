using Api.Dtos;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.Persistence;
using System.Net;

namespace Api;

public class Test(IOptions<CosmosDbSettings> settings, ILogger<Test> logger)
{
    private readonly CosmosDbSettings _settings = settings?.Value ?? throw new ArgumentNullException(nameof(settings));

    [Function("Test")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")]
        HttpRequestData req,
        FunctionContext executionContext)
    {
        logger.LogInformation($"endpoint: '{_settings.Endpoint}', token: '{_settings.AuthKeyOrResourceToken.Substring(Math.Max(0, _settings.AuthKeyOrResourceToken.Length - 10))}'.");

        logger.LogInformation("C# HTTP trigger function processed a request.");
        var success = req.CreateResponse(HttpStatusCode.OK);
        await success.WriteAsJsonAsync(SubmitUrlResponse.Successful("success"));
        return success;
    }
}
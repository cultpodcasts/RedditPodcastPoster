using Api.Dtos;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.Persistence;
using System.Net;

namespace Api;

public class Test
{
    private readonly CosmosDbSettings _settings;
    private readonly ILogger<Test> _logger;

    public Test(IOptions<CosmosDbSettings> settings, ILogger<Test> logger)
    {
        _settings = settings?.Value ?? throw new ArgumentNullException(nameof(settings));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [Function("Test")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")]
        HttpRequestData req,
        FunctionContext executionContext)
    {
        _logger.LogInformation($"endpoint: '{_settings.Endpoint}', token: '{_settings.AuthKeyOrResourceToken.Substring(Math.Max(0, _settings.AuthKeyOrResourceToken.Length - 10))}'.");

        _logger.LogInformation("C# HTTP trigger function processed a request.");
        var success = req.CreateResponse(HttpStatusCode.OK);
        await success.WriteAsJsonAsync(SubmitUrlResponse.Successful("success"));
        return success;
    }
}
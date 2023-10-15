using System.Net;
using System.Web;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace Indexer;

public class YouTubeReceiver
{
    private const string HubChallenge = "hub.challenge";
    private readonly ILogger _logger;

    public YouTubeReceiver(ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<YouTubeReceiver>();
    }

    [Function("YouTubeReceiver")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestData req)
    {
        _logger.LogInformation($"{nameof(YouTubeReceiver)}");
        _logger.LogInformation($"Method: '{req.Method}', Url: '{req.Url}'.");
        var queryString = HttpUtility.ParseQueryString(req.Url.Query);
        var body = await new StreamReader(req.Body).ReadToEndAsync();
        _logger.LogInformation(body);

        if (queryString.AllKeys.Contains(HubChallenge))
        {
            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "text/plain; charset=utf-8");

            response.WriteString(queryString[HubChallenge]);
            return response;
        }

        return req.CreateResponse(HttpStatusCode.Accepted);
    }
}
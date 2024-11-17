using Microsoft.Extensions.Logging;

namespace RedditPodcastPoster.Bluesky.HttpHandlers;

public class LoggingHandler(
    ILogger<LoggingHandler> logger,
    HttpMessageHandler innerHandler) : DelegatingHandler(innerHandler)
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        logger.LogInformation($"url: {request.RequestUri}, method: {request.Method}");

        var response = await base.SendAsync(request, cancellationToken);

        logger.LogInformation(response.ToString());

        return response;
    }
}
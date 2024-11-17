using System.Net;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Bluesky.HttpHandlers;

namespace RedditPodcastPoster.Bluesky.Factories;

public class BlueskyApiHttpClientFactory(
    ILogger<LoggingHandler> logger
) : IBlueskyApiHttpClientFactory
{
    private readonly HttpClient _client = new(
        new LoggingHandler(
            logger,
            new HttpClientHandler
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
            }));

    public HttpClient CreateClient(string name)
    {
        return _client;
    }
}
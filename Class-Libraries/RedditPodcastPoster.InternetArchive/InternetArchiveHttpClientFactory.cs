using Microsoft.Extensions.Logging;

namespace RedditPodcastPoster.InternetArchive;

public class InternetArchiveHttpClientFactory(
    IHttpClientFactory httpClientFactory,
    ILogger<IInternetArchiveHttpClientFactory> logger,
    ILogger<InternetArchiveHttpClient> bbcClientLogger
)
    : IInternetArchiveHttpClientFactory
{
    public IInternetArchiveHttpClient Create()
    {
        logger.LogInformation("Create internet-archive-http-client");
        var httpClient = httpClientFactory.CreateClient();
        httpClient.DefaultRequestHeaders.Add("User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:135.0) Gecko/20100101 Firefox/135.0");
        httpClient.DefaultRequestHeaders.Add("Accept", "text/html");
        httpClient.DefaultRequestHeaders.Add("Accept-Language", "en;q=0.5");
        return new InternetArchiveHttpClient(httpClient, bbcClientLogger);
    }
}
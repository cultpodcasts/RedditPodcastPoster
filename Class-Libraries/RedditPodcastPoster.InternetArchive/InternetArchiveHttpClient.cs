using System.Net;
using Microsoft.Extensions.Logging;

namespace RedditPodcastPoster.InternetArchive;

public class InternetArchiveHttpClient(
    HttpClient httpClient,
    ILogger<InternetArchiveHttpClient> logger) : IInternetArchiveHttpClient
{
    public async Task<HttpResponseMessage> GetAsync(Uri url)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url.PathAndQuery);
        request.Headers.Host = url.Host;
        var response = await httpClient.GetAsync(url);
        if (response.StatusCode == HttpStatusCode.OK)
        {
            logger.LogInformation("internet-archive-http-client get: '{url}', status: {status}.", url,
                response.StatusCode);
        }
        else
        {
            logger.LogError("internet-http-client failure. get: '{url}', status: {status}.", url, response.StatusCode);
        }

        return response;
    }
}
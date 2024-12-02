using System.Net;
using Microsoft.Extensions.Logging;

namespace RedditPodcastPoster.BBC;

public class BBCHttpClient(
    HttpClient httpClient,
    ILogger<BBCHttpClient> logger) : IBBCHttpClient
{
    public async Task<HttpResponseMessage> GetAsync(Uri url)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url.PathAndQuery);
        request.Headers.Host = url.Host;
        var response = await httpClient.GetAsync(url);
        if (response.StatusCode == HttpStatusCode.OK)
        {
            logger.LogInformation("bbc-http-client get: '{url}', status: {status}.", url, response.StatusCode);
        }
        else
        {
            logger.LogError("bbc-http-client failure. get: '{url}', status: {status}.", url, response.StatusCode);
        }

        return response;
    }
}
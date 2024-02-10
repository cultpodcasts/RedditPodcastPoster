using System.Net.Http.Headers;
using Microsoft.Extensions.Logging;

namespace RedditPodcastPoster.PodcastServices.Apple;

public class ApplePodcastHttpClientFactory(
    IHttpClientFactory httpClientFactory,
    IAppleBearerTokenProvider appleBearerTokenProvider,
#pragma warning disable CS9113 // Parameter is unread.
    ILogger<ApplePodcastHttpClientFactory> logger)
#pragma warning restore CS9113 // Parameter is unread.
    : IApplePodcastHttpClientFactory
{
    public async Task<HttpClient> Create()
    {
        var httpClient = httpClientFactory.CreateClient();
        httpClient.BaseAddress = new Uri("https://amp-api.podcasts.apple.com/");
        var token = await appleBearerTokenProvider!.GetHeader();
        httpClient.DefaultRequestHeaders.Authorization = token;
        httpClient.DefaultRequestHeaders.Accept.Clear();
        httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("*/*"));
        httpClient.DefaultRequestHeaders.Referrer = new Uri("https://podcasts.apple.com/");
        httpClient.DefaultRequestHeaders.Add("Origin", "https://podcasts.apple.com");
        httpClient.DefaultRequestHeaders.UserAgent.Clear();
        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:109.0) Gecko/20100101 Firefox/118.0");
        return httpClient;
    }
}
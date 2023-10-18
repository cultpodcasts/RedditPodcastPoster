using System.Net.Http.Headers;
using Microsoft.Extensions.Logging;

namespace RedditPodcastPoster.PodcastServices.Apple;

public class ApplePodcastHttpClientFactory : IApplePodcastHttpClientFactory
{
    private readonly IAppleBearerTokenProvider _appleBearerTokenProvider;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ApplePodcastHttpClientFactory> _logger;

    public ApplePodcastHttpClientFactory(
        IHttpClientFactory httpClientFactory,
        IAppleBearerTokenProvider appleBearerTokenProvider,
        ILogger<ApplePodcastHttpClientFactory> logger)
    {
        _httpClientFactory = httpClientFactory;
        _appleBearerTokenProvider = appleBearerTokenProvider;
        _logger = logger;
    }

    public async Task<HttpClient> Create()
    {
        var httpClient = _httpClientFactory.CreateClient();
        httpClient.BaseAddress = new Uri("https://amp-api.podcasts.apple.com/");
        var token = await _appleBearerTokenProvider!.GetHeader();
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
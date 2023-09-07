using System.Net.Http.Headers;
using Google.Apis.Logging;
using Microsoft.Extensions.Logging;

namespace RedditPodcastPoster.Common.PodcastServices.Apple;

public class AppleBearerTokenProvider : IAppleBearerTokenProvider
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<AppleBearerTokenProvider> _logger;
    private readonly string _token;

    public AppleBearerTokenProvider(
        HttpClient httpClient, 
        ILogger<AppleBearerTokenProvider> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<AuthenticationHeaderValue> GetHeader()
    {
        var podcastsHomepageContent =
            _httpClient.GetAsync("https://www.apple.com/apple-podcasts/").GetAwaiter().GetResult();
        podcastsHomepageContent.EnsureSuccessStatusCode();

        var document = new HtmlAgilityPack.HtmlDocument();
        document.Load(await podcastsHomepageContent.Content.ReadAsStreamAsync());
        var applePodcastTokenNodes= document.DocumentNode.SelectNodes("//meta[@property=\"apple-podcast-token\"]/@content");

        if (!applePodcastTokenNodes.Any() || applePodcastTokenNodes.Count > 1)
        {
            throw new InvalidOperationException($"Found {applePodcastTokenNodes.Count} apple-podcast-token meta-property tags.");
        }

        var token = applePodcastTokenNodes.Single().Attributes["content"].Value;
        return new AuthenticationHeaderValue("Bearer", token);
    }
}
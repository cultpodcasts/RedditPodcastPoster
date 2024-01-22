using System.Net.Http.Headers;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;

namespace RedditPodcastPoster.PodcastServices.Apple;

public class AppleBearerTokenProvider(
    IHttpClientFactory httpClientFactory,
    ILogger<AppleBearerTokenProvider> logger)
    : IAppleBearerTokenProvider
{
    public async Task<AuthenticationHeaderValue> GetHeader()
    {
        var httpClient = httpClientFactory.CreateClient();
        var podcastsHomepageContent = await httpClient.GetAsync("https://www.apple.com/apple-podcasts/");
        podcastsHomepageContent.EnsureSuccessStatusCode();

        var document = new HtmlDocument();
        document.Load(await podcastsHomepageContent.Content.ReadAsStreamAsync());
        var applePodcastTokenNodes =
            document.DocumentNode.SelectNodes("//meta[@property=\"apple-podcast-token\"]/@content");

        if (!applePodcastTokenNodes.Any() || applePodcastTokenNodes.Count > 1)
        {
            throw new InvalidOperationException(
                $"Found {applePodcastTokenNodes.Count} apple-podcast-token meta-property tags.");
        }

        var token = applePodcastTokenNodes.Single().Attributes["content"].Value;
        return new AuthenticationHeaderValue("Bearer", token);
    }
}
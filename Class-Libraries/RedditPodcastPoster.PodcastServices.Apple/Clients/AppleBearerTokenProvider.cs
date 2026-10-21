using System.Net.Http.Headers;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;

namespace RedditPodcastPoster.PodcastServices.Apple.Clients;

public class AppleBearerTokenProvider(
    IHttpClientFactory httpClientFactory,
#pragma warning disable CS9113 // Parameter is unread.
    ILogger<AppleBearerTokenProvider> logger)
#pragma warning restore CS9113 // Parameter is unread.
    : IAppleBearerTokenProvider
{
    public async Task<AuthenticationHeaderValue> GetHeader(CancellationToken cancellationToken = default)
    {
        var httpClient = httpClientFactory.CreateClient();
        var podcastsHomepageContent =
            await httpClient.GetAsync("https://www.apple.com/apple-podcasts/", cancellationToken);
        podcastsHomepageContent.EnsureSuccessStatusCode();

        var document = new HtmlDocument();
        document.Load(await podcastsHomepageContent.Content.ReadAsStreamAsync(cancellationToken));
        var applePodcastTokenNodes =
            document.DocumentNode.SelectNodes("//meta[@property=\"apple-podcast-token\"]/@content");

        if (applePodcastTokenNodes is not { Count: 1 })
        {
            throw new InvalidOperationException(
                $"Found {applePodcastTokenNodes?.Count ?? 0} apple-podcast-token meta-property tags.");
        }

        var token = applePodcastTokenNodes[0].Attributes["content"]?.Value;
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new InvalidOperationException("apple-podcast-token meta-property has no content.");
        }

        return new AuthenticationHeaderValue("Bearer", token);
    }
}
using Azure;
using Azure.AI.TextAnalytics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.AI.Configuration;

namespace RedditPodcastPoster.AI.Factories;

public class TextAnalyticsClientFactory(
    IOptions<TextAnalyticsSettings> options,
#pragma warning disable CS9113 // Parameter is unread.
    ILogger<TextAnalyticsClientFactory> logger)
#pragma warning restore CS9113 // Parameter is unread.
    : ITextAnalyticsClientFactory
{
    private readonly TextAnalyticsSettings _options = options.Value;

    public TextAnalyticsClient Create()
    {
        Uri endpoint = new(_options.EndPoint);
        AzureKeyCredential credential = new(_options.ApiKey);
        TextAnalyticsClient client = new(endpoint, credential);
        return client;
    }
}
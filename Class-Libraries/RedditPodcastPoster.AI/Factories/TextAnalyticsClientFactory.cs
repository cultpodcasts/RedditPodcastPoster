using Azure;
using Azure.AI.TextAnalytics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.AI.Configuration;

namespace RedditPodcastPoster.AI.Factories;

public class TextAnalyticsClientFactory(
    IOptions<TextAnalyticsSettings> options,
    ILogger<TextAnalyticsClientFactory> logger)
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
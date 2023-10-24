using Azure;
using Azure.AI.TextAnalytics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.AI.Configuration;

namespace RedditPodcastPoster.AI.Factories;

public class TextAnalyticsClientFactory : ITextAnalyticsClientFactory
{
    private readonly ILogger<TextAnalyticsClientFactory> _logger;
    private readonly TextAnalyticsSettings _options;

    public TextAnalyticsClientFactory(
        IOptions<TextAnalyticsSettings> options,
        ILogger<TextAnalyticsClientFactory> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public TextAnalyticsClient Create()
    {
        Uri endpoint = new(_options.EndPoint);
        AzureKeyCredential credential = new(_options.ApiKey);
        TextAnalyticsClient client = new(endpoint, credential);
        return client;
    }
}
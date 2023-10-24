using Azure.AI.TextAnalytics;

namespace RedditPodcastPoster.AI.Factories;

public interface ITextAnalyticsClientFactory
{
    public TextAnalyticsClient Create();
}
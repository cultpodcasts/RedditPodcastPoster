using Azure.AI.TextAnalytics;

namespace RedditPodcastPoster.AI.Factories;

public interface IClassifyActionFactory
{
    SingleLabelClassifyAction Create();
}
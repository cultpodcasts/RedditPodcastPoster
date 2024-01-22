using Azure.AI.TextAnalytics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.AI.Configuration;

namespace RedditPodcastPoster.AI.Factories;

public class ClassifyActionFactory(
    IOptions<PodcastSubjectAIModelSettings> options,
    ILogger<ClassifyActionFactory> logger)
    : IClassifyActionFactory
{
    private readonly PodcastSubjectAIModelSettings _options = options.Value;

    public SingleLabelClassifyAction Create()
    {
        return new SingleLabelClassifyAction(_options.Project, _options.Deployment);
    }
}
using Azure.AI.TextAnalytics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.AI.Configuration;

namespace RedditPodcastPoster.AI.Factories;

public class ClassifyActionFactory : IClassifyActionFactory
{
    private readonly ILogger<ClassifyActionFactory> _logger;
    private readonly PodcastSubjectAIModelSettings _options;

    public ClassifyActionFactory(
        IOptions<PodcastSubjectAIModelSettings> options,
        ILogger<ClassifyActionFactory> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public SingleLabelClassifyAction Create()
    {
        return new SingleLabelClassifyAction(_options.Project, _options.Deployment);
    }
}
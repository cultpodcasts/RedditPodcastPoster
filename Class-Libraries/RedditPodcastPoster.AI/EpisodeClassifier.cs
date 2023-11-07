using Azure.AI.TextAnalytics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.AI.Configuration;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence.Abstractions;

public class EpisodeClassifier : IEpisodeClassifier
{
    private readonly ClassificationSettings _classificationOptions;
    private readonly ILogger<EpisodeClassifier> _logger;
    private readonly SingleLabelClassifyAction _singleLabelClassifyAction;
    private readonly ICachedSubjectRepository _subjectRepository;
    private readonly TextAnalyticsClient _textAnalyticsClient;

    public EpisodeClassifier(
        ICachedSubjectRepository subjectRepository,
        TextAnalyticsClient textAnalyticsClient,
        SingleLabelClassifyAction singleLabelClassifyAction,
        IOptions<ClassificationSettings> classificationOptions,
        ILogger<EpisodeClassifier> logger)
    {
        _subjectRepository = subjectRepository;
        _textAnalyticsClient = textAnalyticsClient;
        _singleLabelClassifyAction = singleLabelClassifyAction;
        _classificationOptions = classificationOptions.Value;
        _logger = logger;
    }

    public async Task CategoriseEpisode(Episode episode)
    {
        if (string.IsNullOrWhiteSpace(episode.Category))
        {
            var episodeText = $"{episode.Title}\n\r{episode.Description}";
            var operation =
                await _textAnalyticsClient.StartAnalyzeActionsAsync(
                    new[] {episodeText},
                    new TextAnalyticsActions {SingleLabelClassifyActions = new[] {_singleLabelClassifyAction}});
            await operation.WaitForCompletionAsync();

            if (operation is {ActionsFailed: 0, ActionsSucceeded: 1})
            {
                var result = await operation.Value.AsPages().ToListAsync();
                if (result.Count > 1)
                {
                    _logger.LogInformation($"Multiple-results: '{result.Count}'.");
                }

                var page =
                    result.First()
                        .Values.First()
                        .SingleLabelClassifyResults.First()
                        .DocumentsResults.First()
                        .ClassificationCategories.First();
                var label = page.Category;
                var confidence = page.ConfidenceScore;
                if (confidence >= _classificationOptions.MinimumConfidence)
                {
                    var matchingSubject =
                        (await _subjectRepository.GetAll(Subject.PartitionKey)).SingleOrDefault(x =>
                            x.Name.ToLower() == label.ToLower());
                    episode.Category = matchingSubject?.Name;
                }
            }
            else
            {
                _logger.LogError($"Failure to classify logger with title '{episode.Title}' and id '{episode.Id}'.");
            }
        }
    }
}
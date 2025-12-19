using Azure.AI.TextAnalytics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.AI.Configuration;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence.Abstractions;

public class EpisodeClassifier(
    ISubjectRepository subjectRepository,
    TextAnalyticsClient textAnalyticsClient,
    SingleLabelClassifyAction singleLabelClassifyAction,
    IOptions<ClassificationSettings> classificationOptions,
    ILogger<EpisodeClassifier> logger)
    : IEpisodeClassifier
{
    private readonly ClassificationSettings _classificationOptions = classificationOptions.Value;

    public async Task CategoriseEpisode(Episode episode)
    {
        //if (string.IsNullOrWhiteSpace(episode.Category))
        //{
        var episodeText = $"{episode.Title}\n\r{episode.Description}";
        var operation =
            await textAnalyticsClient.StartAnalyzeActionsAsync(
                new[] {episodeText},
                new TextAnalyticsActions {SingleLabelClassifyActions = new[] {singleLabelClassifyAction}});
        await operation.WaitForCompletionAsync();

        if (operation is {ActionsFailed: 0, ActionsSucceeded: 1})
        {
            var result = await operation.Value.AsPages().ToListAsync();
            if (result.Count > 1)
            {
                logger.LogInformation("Multiple-results: '{ResultCount}'.", result.Count);
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
                var matchingSubject = await subjectRepository.GetByName(label);
                //episode.Category = matchingSubject?.Name;
            }
        }
        else
        {
            logger.LogError("Failure to classify logger with title '{EpisodeTitle}' and id '{EpisodeId}'.", episode.Title, episode.Id);
        }
        //}
    }
}
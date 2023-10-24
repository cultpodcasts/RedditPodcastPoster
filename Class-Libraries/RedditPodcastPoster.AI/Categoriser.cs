using Azure.AI.TextAnalytics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.AI;
using RedditPodcastPoster.AI.Configuration;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence.Abstractions;
using RedditPodcastPoster.Subjects;

public class Categoriser : ICategoriser
{
    private readonly ClassificationSettings _classificationOptions;
    private readonly ILogger<Categoriser> _logger;
    private readonly SingleLabelClassifyAction _singleLabelClassifyAction;
    private readonly ICachedSubjectRepository _subjectRepository;
    private readonly ISubjectService _subjectService;
    private readonly TextAnalyticsClient _textAnalyticsClient;

    public Categoriser(
        TextAnalyticsClient textAnalyticsClient,
        SingleLabelClassifyAction singleLabelClassifyAction,
        IOptions<ClassificationSettings> classificationOptions,
        ICachedSubjectRepository subjectRepository,
        ISubjectService subjectService,
        ILogger<Categoriser> logger)
    {
        _textAnalyticsClient = textAnalyticsClient;
        _singleLabelClassifyAction = singleLabelClassifyAction;
        _subjectRepository = subjectRepository;
        _subjectService = subjectService;
        _classificationOptions = classificationOptions.Value;
        _logger = logger;
    }

    public async Task<bool> Categorise(Episode episode)
    {
        var originalSubject = episode.Subject;
        var originalCategory = episode.Category;

        var subjects = await _subjectService.Match(episode, false);
        var subject =
            subjects
                .GroupBy(y => y)
                .OrderByDescending(g => g.Count())
                .SelectMany(g => g).ToList()
                .FirstOrDefault();
        episode.Subject = subject;

        var updated = episode.Subject != originalSubject;
        if (!updated)
        {
            var descriptionSubjects = await _subjectService.Match(episode, true);
            var descriptionSubject =
                descriptionSubjects
                    .GroupBy(y => y)
                    .OrderByDescending(g => g.Count())
                    .SelectMany(g => g).ToList()
                    .FirstOrDefault();
            episode.Subject = descriptionSubject;
        }

        updated = episode.Subject != originalSubject;


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
                    result.FirstOrDefault()
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
                    episode.Category = matchingSubject.Name;
                }
            }
            else
            {
                _logger.LogError($"Failure to classify episode with title '{episode.Title}' and id '{episode.Id}'.");
            }
        }
        var updatedCategory = episode.Category != originalCategory;

        return updated || updatedCategory;
    }
}
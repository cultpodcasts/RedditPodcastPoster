using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Reddit;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence.Abstractions;
using RedditPodcastPoster.Reddit;
using RedditPodcastPoster.Subjects;

namespace EnrichSubjectRedditFlairs;

public class RedditFlairsProcessor
{
    private readonly ICachedSubjectRepository _cachedSubjectRepository;
    private readonly ILogger<RedditFlairsProcessor> _logger;
    private readonly RedditClient _redditClient;
    private readonly IRepository<Subject> _repository;
    private readonly ISubjectCleanser _subjectCleanser;
    private readonly SubredditSettings _subredditSettings;

    public RedditFlairsProcessor(
        RedditClient redditClient,
        IRepository<Subject> repository,
        ISubjectCleanser subjectCleanser,
        IOptions<SubredditSettings> subredditSettings,
        ICachedSubjectRepository cachedSubjectRepository,
        ILogger<RedditFlairsProcessor> logger)
    {
        _redditClient = redditClient;
        _repository = repository;
        _subjectCleanser = subjectCleanser;
        _cachedSubjectRepository = cachedSubjectRepository;
        _subredditSettings = subredditSettings.Value;
        _logger = logger;
    }

    public async Task Run()
    {
        var subjects = await _cachedSubjectRepository.GetAll(Subject.PartitionKey);
        var subreddit = _redditClient.Subreddit(_subredditSettings.SubredditName);
        var linkFlairs = subreddit.Flairs.LinkFlairV2;
        foreach (var flair in linkFlairs)
        {
            var (unmatched, cleansedFlair) = await _subjectCleanser.CleanSubjects(new List<string> {flair.Text});
            if (unmatched)
            {
                _logger.LogError($"Unmatched flair '{flair.Text}'.");
            }
            else
            {
                if (!cleansedFlair.Any() || cleansedFlair.Count > 1)
                {
                    if (!cleansedFlair.Any())
                    {
                        _logger.LogError(
                            $"Matched flair with subject, but no cleansed-subjects. Flair-text: '{flair.Text}'.");
                    }
                    else
                    {
                        _logger.LogError(
                            $"Multiple cleansed flairs for flair '{flair.Text}'. Cleansed-subjects: {string.Join(",", cleansedFlair.Select(x => $"'{x}'"))}. Taking first.");
                        cleansedFlair = cleansedFlair.Take(1).ToList();
                    }
                }

                var subject = subjects.SingleOrDefault(x => x.Name == cleansedFlair.Single());
                if (subject != null)
                {
                    if (subject.RedditFlairTemplateId == null)
                    {
                        _logger.LogInformation($"Updating '{subject.Name}' with template-id '{flair.Id}'.");
                        var redditFlairTemplateId = Guid.Parse(flair.Id);
                        subject.RedditFlairTemplateId = redditFlairTemplateId;
                        await _repository.Save(subject);
                    }
                }
                else
                {
                    _logger.LogError($"No subject found for cleansed-flair '{cleansedFlair.Single()}'.");
                }
            }
        }
    }
}
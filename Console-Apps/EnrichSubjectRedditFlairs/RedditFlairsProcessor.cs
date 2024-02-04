using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Reddit;
using RedditPodcastPoster.Persistence.Abstractions;
using RedditPodcastPoster.Reddit;
using RedditPodcastPoster.Subjects;

namespace EnrichSubjectRedditFlairs;

public class RedditFlairsProcessor(
    RedditClient redditClient,
    ISubjectRepository repository,
    ISubjectCleanser subjectCleanser,
    IOptions<SubredditSettings> subredditSettings,
    ISubjectRepository subjectRepository,
    ILogger<RedditFlairsProcessor> logger)
{
    private readonly SubredditSettings _subredditSettings = subredditSettings.Value;

    public async Task Run()
    {
        var subreddit = redditClient.Subreddit(_subredditSettings.SubredditName);
        var linkFlairs = subreddit.Flairs.LinkFlairV2;
        foreach (var flair in linkFlairs)
        {
            var (unmatched, cleansedFlair) = await subjectCleanser.CleanSubjects(new List<string> {flair.Text});
            if (unmatched)
            {
                logger.LogError($"Unmatched flair '{flair.Text}'.");
            }
            else
            {
                if (!cleansedFlair.Any() || cleansedFlair.Count > 1)
                {
                    if (!cleansedFlair.Any())
                    {
                        logger.LogError(
                            $"Matched flair with subject, but no cleansed-subjects. Flair-text: '{flair.Text}'.");
                    }
                    else
                    {
                        logger.LogError(
                            $"Multiple cleansed flairs for flair '{flair.Text}'. Cleansed-subjects: {string.Join(",", cleansedFlair.Select(x => $"'{x}'"))}. Taking first.");
                        cleansedFlair = cleansedFlair.Take(1).ToList();
                    }
                }

                var subject = await subjectRepository.GetByName(cleansedFlair.Single());
                if (subject != null)
                {
                    if (subject.RedditFlairTemplateId == null)
                    {
                        logger.LogInformation($"Updating '{subject.Name}' with template-id '{flair.Id}'.");
                        var redditFlairTemplateId = Guid.Parse(flair.Id);
                        subject.RedditFlairTemplateId = redditFlairTemplateId;
                        await repository.Save(subject);
                    }
                }
                else
                {
                    logger.LogError($"No subject found for cleansed-flair '{cleansedFlair.Single()}'.");
                }
            }
        }
    }
}
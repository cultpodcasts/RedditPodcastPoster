using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Reddit;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence.Abstractions;
using RedditPodcastPoster.Reddit;
using RedditPodcastPoster.Subjects;

namespace SubjectSeeder;

public class SubjectsSeeder(
    ISubjectRepository subjectRepository,
    ISubjectService subjectService,
    RedditClient redditClient,
    IOptions<SubredditSettings> subredditSettings,
    ILogger<SubjectsSeeder> logger)
{
    private readonly SubredditSettings _subredditSettings = subredditSettings.Value;

    public async Task Run(SubjectRequest subjectRequest)
    {
        var subject = SubjectFactory.Create(
            subjectRequest.Name,
            subjectRequest.Aliases,
            subjectRequest.AssociatedSubjects,
            subjectRequest.HashTags);

        var match = await subjectService.Match(subject);

        if (match == null)
        {
            if (!string.IsNullOrWhiteSpace(subjectRequest.Flair))
            {
                var createdFlair = await redditClient
                    .Subreddit(_subredditSettings.SubredditName)
                    .Flairs
                    .CreateLinkFlairTemplateV2Async(subjectRequest.Flair);
                var createdFlairId = createdFlair!.Id;
                subject.RedditFlairTemplateId = Guid.Parse(createdFlairId);
            }

            await subjectRepository.Save(subject);
        }
        else
        {
            logger.LogError($"Subject '{subject.Name}' matches subject '{match.Name}'.");
        }
    }
}
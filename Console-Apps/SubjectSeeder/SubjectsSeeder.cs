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
    IRecycledFlareIdProvider recycledFlareIdProvider,
    IOptions<SubredditSettings> subredditSettings,
    ILogger<SubjectsSeeder> logger)
{
    private const int MaxSubredditFlairs = 320;
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
                if (subjectRequest.CreateFlair)
                {
                    var flairs = redditClient.Subreddit(_subredditSettings.SubredditName).Flairs.GetFlairList();
                    if (flairs.Count >= MaxSubredditFlairs)
                    {
                        throw new ArgumentException(
                            $"Cannot create new flare. Subreddit has hit limit of flairs at '{MaxSubredditFlairs}'.");
                    }

                    var createdFlair = await redditClient
                        .Subreddit(_subredditSettings.SubredditName)
                        .Flairs
                        .CreateLinkFlairTemplateV2Async(subjectRequest.Flair);
                    var createdFlairId = createdFlair!.Id;
                    subject.RedditFlairTemplateId = Guid.Parse(createdFlairId);
                }
                else
                {
                    if (string.IsNullOrWhiteSpace(subjectRequest.RecycledFlairName))
                    {
                        throw new ArgumentNullException(
                            $"Recycled Flair Name is missing. Valid names: {string.Join(", ", recycledFlareIdProvider.GetKeys().Select(x => $"'{x}'"))}.",
                            nameof(subjectRequest.RecycledFlairName));
                    }

                    var flairId = recycledFlareIdProvider.GetId(subjectRequest.RecycledFlairName);
                    subject.RedditFlairTemplateId = flairId;
                    subject.RedditFlareText = subjectRequest.Flair;
                }
            }

            await subjectRepository.Save(subject);
        }
        else
        {
            logger.LogError($"Subject '{subject.Name}' matches subject '{match.Name}'.");
        }
    }
}
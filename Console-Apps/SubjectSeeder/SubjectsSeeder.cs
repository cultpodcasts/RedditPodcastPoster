using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Reddit;
using Reddit.Inputs.Flair;
using RedditPodcastPoster.ContentPublisher;
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
    IContentPublisher contentPublisher,
    ILogger<SubjectsSeeder> logger)
{
    private const int MaxSubredditFlairs = 320;
    private readonly SubredditSettings _subredditSettings = subredditSettings.Value;

    public async Task Run(SubjectRequest subjectRequest)
    {
        Subject? match = null;
        if (!subjectRequest.Publish)
        {
            var subject = SubjectFactory.Create(
                subjectRequest.Name,
                subjectRequest.Aliases,
                subjectRequest.AssociatedSubjects,
                subjectRequest.HashTags);

            match = await subjectService.Match(subject);

            if (match == null)
            {
                if (!string.IsNullOrWhiteSpace(subjectRequest.Flair) ||
                    !string.IsNullOrWhiteSpace(subjectRequest.RecycledFlairName))
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

                        // verify flare is user editable
                        var subredditFlairs = redditClient
                            .Subreddit(_subredditSettings.SubredditName)
                            .Flairs
                            .GetLinkFlairV2();
                        var flair = subredditFlairs.SingleOrDefault(x => x.Id == flairId.ToString());
                        if (flair == null)
                        {
                            throw new InvalidOperationException($"Unable to find subreddit-flair with id '{flairId}'.");
                        }

                        if (!flair.TextEditable)
                        {
                            var subjectUsingFlair =
                                await subjectRepository.GetBy(x => x.RedditFlairTemplateId == flairId);
                            if (subjectUsingFlair != null)
                            {
                                subjectUsingFlair.RedditFlareText = flair.Text;
                                await subjectRepository.Save(subjectUsingFlair);
                                logger.LogInformation(
                                    $"Adjusted subject '{subjectUsingFlair.Name}' with id '{subjectUsingFlair.Id}' to have  {nameof(subjectUsingFlair.RedditFlareText)}='{flair.Text}'.");
                            }

                            flair.TextEditable = true;
                            var updateResult = await redditClient
                                .Subreddit(_subredditSettings.SubredditName)
                                .Flairs
                                .UpdateLinkFlairTemplateV2Async(new FlairTemplateV2Input
                                {
                                    background_color = flair.BackgroundColor,
                                    flair_template_id = flair.Id,
                                    flair_type = flair.Type,
                                    text = flair.Text,
                                    text_color = flair.TextColor,
                                    text_editable = true
                                });
                            if (!updateResult.TextEditable)
                            {
                                logger.LogError($"Error updating flare '{flair.Text}' with id '{flair.Id}'.");
                            }
                        }


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

        if (match == null || subjectRequest.Publish)
        {
            await contentPublisher.PublishSubjects();
        }
    }
}
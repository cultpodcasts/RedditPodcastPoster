using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Reddit.Inputs.Flair;
using Subject = RedditPodcastPoster.Models.Subjects.Subject;
using Api.Models;
using RedditPodcastPoster.Models.Subjects;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.Reddit.Clients;
using RedditPodcastPoster.Reddit.Configuration;

namespace Api.Services.Subjects;

public class SubjectChangeApplier(
    ISubjectRepository subjectRepository,
    IAdminRedditClient redditClient,
    IOptions<SubredditSettings> subredditSettings,
    ILogger<SubjectChangeApplier> logger)
{
    private readonly SubredditSettings _subredditSettings = subredditSettings.Value;

    public async Task Apply(Subject subject, SubjectChangeRequest change)
    {
        if (change.Aliases != null)
        {
            subject.Aliases = !change.Aliases.Any() ? null : change.Aliases.Select(x => x.Trim()).ToArray();
        }

        if (change.AssociatedSubjects != null)
        {
            subject.AssociatedSubjects = !change.AssociatedSubjects.Any()
                ? null
                : change.AssociatedSubjects.Select(x => x.Trim()).ToArray();
        }

        if (change.EnrichmentHashTags != null)
        {
            subject.EnrichmentHashTags = !change.EnrichmentHashTags.Any()
                ? null
                : change.EnrichmentHashTags.Select(x => x.Trim()).ToArray();
        }

        if (change.HashTag != null)
        {
            subject.HashTag = change.HashTag == string.Empty ? null : change.HashTag.Trim();
        }

        if (change.RedditFlairTemplateId != null)
        {
            if (change.RedditFlairTemplateId == Guid.Empty)
            {
                subject.RedditFlairTemplateId = null;
            }
            else
            {
                subject.RedditFlairTemplateId = change.RedditFlairTemplateId;
                await UseFlair(subject, change.RedditFlairTemplateId.Value);
            }
        }

        if (change.RedditFlareText != null)
        {
            subject.RedditFlareText = change.RedditFlareText == string.Empty ? null : change.RedditFlareText.Trim();
        }

        if (change.SubjectType != null)
        {
            subject.SubjectType = change.SubjectType != SubjectType.Unset ? change.SubjectType : null;
        }

        if (change.KnownTerms != null)
        {
            subject.KnownTerms = change.KnownTerms.Length > 0 ? change.KnownTerms : null;
        }
    }

    private async Task UseFlair(Subject subject, Guid flairId)
    {
        var subredditFlairs = redditClient.Client
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
            var subjectsUsingFlair =
                await subjectRepository.GetAllBy(x => x.RedditFlairTemplateId == flairId).ToListAsync();
            if (subjectsUsingFlair.Count == 1)
            {
                var subjectUsingFlair = subjectsUsingFlair.Single();
                subjectUsingFlair.RedditFlareText = flair.Text;
                await subjectRepository.Save(subjectUsingFlair);
                logger.LogInformation(
                    "Adjusted subject '{subjectUsingFlairName}' with id '{subjectUsingFlairId}' to have  {nameofRedditFlareText}='{flairText}'.",
                    subjectUsingFlair.Name, subjectUsingFlair.Id, nameof(subjectUsingFlair.RedditFlareText),
                    flair.Text);
            }

            flair.TextEditable = true;
            var updateResult = await redditClient.Client
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
                logger.LogError("Error updating flare '{flairText}' with id '{flairId}'.", flair.Text, flair.Id);
            }
        }

        subject.RedditFlairTemplateId = flairId;
        if (!string.IsNullOrWhiteSpace(subject.RedditFlareText))
        {
            subject.RedditFlareText = subject.Name;
        }
    }
}

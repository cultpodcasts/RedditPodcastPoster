using Microsoft.Extensions.Logging;
using Subject = RedditPodcastPoster.Models.Subjects.Subject;
using Api.Models;
using RedditPodcastPoster.Models.Subjects;

namespace Api.Services.Subjects;

/// <summary>
/// Applies subject change requests to Cosmos only.
/// Live Reddit flair template sync retired with Reddit.NET.
/// </summary>
public class SubjectChangeApplier(ILogger<SubjectChangeApplier> logger)
{
    public Task Apply(Subject subject, SubjectChangeRequest change)
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
                logger.LogInformation(
                    "Persisting Reddit flair template id '{FlairId}' for subject '{SubjectName}' without live Reddit sync (Reddit.NET retired).",
                    change.RedditFlairTemplateId,
                    subject.Name);
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

        return Task.CompletedTask;
    }
}

using Api.Models;
using Api.Services.Episodes;
using RedditPodcastPoster.Models.Catalogue;

namespace Api.Services.Catalogue;

/// <summary>
/// Applies the curator publisher fields shared by news organisations, TV shows and films.
/// Same null / empty rules as <c>PodcastChangeApplier</c>: null = unchanged, "" clears a string, [] clears a list.
/// </summary>
public static class PublisherChangeApplier
{
    public static void Apply(Publisher publisher, PublisherFieldsChange change)
    {
        if (change.Language != null)
        {
            publisher.Language = EpisodeChangeApplier.NormaliseEpisodeLanguage(change.Language);
        }

        if (change.TwitterHandle != null)
        {
            publisher.TwitterHandle = change.TwitterHandle.Trim();
        }

        if (change.BlueskyHandle != null)
        {
            publisher.BlueskyHandle = string.IsNullOrWhiteSpace(change.BlueskyHandle)
                ? null
                : change.BlueskyHandle.Trim();
        }

        if (change.HashTag != null)
        {
            publisher.HashTag = PlayableChangeApplier.NormaliseHashTag(change.HashTag);
        }

        if (change.EnrichmentHashTags != null)
        {
            publisher.EnrichmentHashTags = change.EnrichmentHashTags.Length == 0
                ? null
                : change.EnrichmentHashTags.Select(x => x.Trim()).ToArray();
        }

        if (change.SearchTerms != null)
        {
            publisher.SearchTerms = PlayableChangeApplier.NormaliseSearchTerms(change.SearchTerms);
        }

        if (change.KnownTerms != null)
        {
            publisher.KnownTerms = change.KnownTerms.Length > 0 ? change.KnownTerms : null;
        }

        if (change.DefaultSubject != null)
        {
            publisher.DefaultSubject = change.DefaultSubject == string.Empty ? null : change.DefaultSubject;
        }

        if (change.IgnoredSubjects != null)
        {
            publisher.IgnoredSubjects = change.IgnoredSubjects;
        }

        if (change.IgnoredAssociatedSubjects != null)
        {
            publisher.IgnoredAssociatedSubjects = change.IgnoredAssociatedSubjects;
        }
    }
}

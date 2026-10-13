using RedditPodcastPoster.Models.Episodes;

namespace RedditPodcastPoster.Episodes.Matching.Candidates;

/// <summary>
/// Classifies subjects on the probe and candidates for multi-criteria catalogue scoring, and returns the
/// effective ignored-subject list (podcast + language). Implemented in RedditPodcastPoster.Subjects.
/// </summary>
public interface ICatalogueSubjectClassifier
{
    Task<IReadOnlyList<string>?> ClassifyAsync(
        Episode probe,
        IReadOnlyList<Episode> candidates,
        CatalogueLookup lookup,
        CancellationToken cancellationToken);
}

/// <summary>Fallback when subject services are not registered: no classification, podcast ignore list only.</summary>
public sealed class NoCatalogueSubjectClassifier : ICatalogueSubjectClassifier
{
    public Task<IReadOnlyList<string>?> ClassifyAsync(
        Episode probe,
        IReadOnlyList<Episode> candidates,
        CatalogueLookup lookup,
        CancellationToken cancellationToken) =>
        Task.FromResult(lookup.IgnoredSubjects);
}

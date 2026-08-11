using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Subjects.Models;

namespace RedditPodcastPoster.Subjects.Factories;

public interface ISubjectEnrichmentOptionsFactory
{
    /// <summary>
    /// Builds enrichment options with podcast ignored-subjects unioned with language-level
    /// ignored subjects from the title-casing document for the episode/podcast language.
    /// </summary>
    Task<SubjectEnrichmentOptions> CreateAsync(
        Podcast podcast,
        Episode? episode = null,
        CancellationToken cancellationToken = default);
}

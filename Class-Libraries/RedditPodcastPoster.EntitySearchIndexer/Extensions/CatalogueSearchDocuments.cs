using RedditPodcastPoster.EntitySearchIndexer;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.Search.Models;

namespace RedditPodcastPoster.EntitySearchIndexer.Extensions;

/// <summary>
/// Same-id search swap documents for catalogue playables: kind transfer, Move to film and submit-url indexing.
/// MergeOrUpload keeps the episode (or Film) GUID as the search key.
/// </summary>
public static class CatalogueSearchDocuments
{
    public static IReadOnlyList<EpisodeSearchRecord> FromPodcastEpisodes(
        Podcast podcast,
        IEnumerable<Episode> episodes,
        string contentKind)
    {
        ArgumentNullException.ThrowIfNull(podcast);
        ArgumentNullException.ThrowIfNull(episodes);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentKind);
        if (contentKind is not (
            SearchContentKind.NewsReport or
            SearchContentKind.TvShowEpisode or
            SearchContentKind.Film))
        {
            throw new ArgumentOutOfRangeException(
                nameof(contentKind),
                contentKind,
                "Search swap is NewsReport, Film, or TvShowEpisode.");
        }

        var documents = new List<EpisodeSearchRecord>();
        foreach (var episode in episodes)
        {
            // Same rule as the episode indexer: a removed episode, or one on a removed podcast, has no document.
            // Kind transfer passes every episode, including removed ones that must stay out of the index.
            if (EpisodeSearchIndexEligibility.ShouldExcludeFromSearch(podcast, episode))
            {
                continue;
            }

            var record = new PodcastEpisode(podcast, episode)
                .ToEpisodeSearchRecord(includeUnifiedPlayableFields: true);
            record.ContentKind = contentKind;
            // A film has no parent. A TV show or news organisation keeps the podcast's own name,
            // the same name CatalogueParentKindMapper gives the new parent.
            record.SeriesName = contentKind == SearchContentKind.Film
                ? null
                : CatalogueParentKindMapper.ParentName(podcast);

            documents.Add(record);
        }

        return documents;
    }
}

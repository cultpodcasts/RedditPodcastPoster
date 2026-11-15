using RedditPodcastPoster.EntitySearchIndexer;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.Search.Models;

namespace RedditPodcastPoster.EntitySearchIndexer.Extensions;

/// <summary>
/// Same-id search swap documents for catalogue playables: kind transfer, Move to film and submit-url indexing.
/// The episode (or Film) GUID stays the search key. Films are uploaded (replaced) so a null
/// <c>seriesName</c> or <c>podcastName</c> clears a stale uploader channel; other kinds are
/// merge-or-uploaded (RPP #1428).
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
            // A film has no parent, so seriesName and podcastName must be cleared (the write uses
            // Upload so the omitted nulls actually leave the index; a merge would keep the uploader
            // channel, RPP #1428). A TV show or news organisation keeps the podcast's own name,
            // the same name CatalogueParentKindMapper gives the new parent.
            if (contentKind == SearchContentKind.Film)
            {
                record.SeriesName = null;
                record.PodcastName = null;
            }
            else
            {
                record.SeriesName = CatalogueParentKindMapper.ParentName(podcast);
            }

            documents.Add(record);
        }

        return documents;
    }
}

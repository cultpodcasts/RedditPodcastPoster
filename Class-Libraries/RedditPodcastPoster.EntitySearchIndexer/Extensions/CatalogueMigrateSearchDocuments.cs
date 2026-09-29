using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.Search.Models;

namespace RedditPodcastPoster.EntitySearchIndexer.Extensions;

/// <summary>
/// Same-id search swap documents for catalogue migrate / kind transfer.
/// MergeOrUpload keeps the episode (or Film) GUID as the search key.
/// </summary>
public static class CatalogueMigrateSearchDocuments
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
            var record = new PodcastEpisode(podcast, episode)
                .ToEpisodeSearchRecord(includeUnifiedPlayableFields: true);
            record.ContentKind = contentKind;
            if (contentKind == SearchContentKind.Film)
            {
                record.SeriesName = null;
            }

            documents.Add(record);
        }

        return documents;
    }
}

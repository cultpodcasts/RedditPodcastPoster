namespace RedditPodcastPoster.Models.Podcasts;

/// <summary>
/// The YouTube discovery settings a podcast, TV show or news organisation shares, so the indexer reads and writes
/// YouTube hints the same way for all three. <see cref="Podcast"/> implements it directly; a
/// <see cref="TvShows.TvShow"/> or <see cref="News.NewsOrganisation"/> implements it through its <c>indexing</c>
/// block (<see cref="Catalogue.CatalogueIndexingSource"/>).
/// </summary>
public interface IYouTubeIndexingSettings
{
    string? YouTubeChannelId { get; }

    string? YouTubePlaylistId { get; }

    PlaylistOrder? YouTubePlaylistOrder { get; }

    bool? YouTubePlaylistQueryIsExpensive { get; set; }

    /// <summary>Indexer-owned hints for the current playlist. Null until a full walk of a large playlist.</summary>
    YouTubeIndexingHints? YouTubeIndexingHints { get; set; }
}

/// <summary>
/// Something that carries <see cref="IYouTubeIndexingSettings"/>: a podcast is its own settings, a TV show or news
/// organisation returns its <c>indexing</c> block (null when it has none).
/// </summary>
public interface IHasYouTubeIndexingSettings
{
    IYouTubeIndexingSettings? YouTubeIndexing { get; }
}

public static class YouTubeIndexingSettingsExtensions
{
    /// <summary>
    /// The stored hints when they describe the current playlist; null when there are none or they were written for
    /// another playlist (a curator changed <c>youTubePlaylistId</c>).
    /// </summary>
    public static YouTubeIndexingHints? CurrentYouTubeIndexingHints(this IYouTubeIndexingSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var hints = settings.YouTubeIndexingHints;
        return hints != null &&
               !string.IsNullOrWhiteSpace(settings.YouTubePlaylistId) &&
               string.Equals(hints.PlaylistId, settings.YouTubePlaylistId, StringComparison.Ordinal)
            ? hints
            : null;
    }
}

namespace RedditPodcastPoster.Models.Catalogue;

/// <summary>
/// Catalogue kinds that store a compact <c>youTubeId</c> for Azure Search Watch
/// (Film, TvShowEpisode, NewsReport). Synced from <c>services.youtube.url</c>.
/// Podcast <c>Episode</c> is not this — nested <c>ids.youtube</c> only; leftover
/// Cosmos top-level <c>youTubeId</c> is ignored (no member).
/// Extends <see cref="IPlayable"/> so sync can read <see cref="IPlayable.Services"/>.
/// </summary>
public interface ICatalogueYouTubeItem : IPlayable
{
    /// <summary>Compact YouTube video id; omit when unknown.</summary>
    string? YouTubeId { get; set; }
}

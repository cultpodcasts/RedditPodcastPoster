using RedditPodcastPoster.Models.ContentKinds;
using RedditPodcastPoster.Models.Podcasts;

namespace RedditPodcastPoster.Models.Episodes;

/// <summary>
/// Pairs a detached `Podcast` with a detached `Episode`.
/// For social posting, a TV episode, news report or film is projected into the same shape with
/// <see cref="ContentKind"/> set: <see cref="Podcast"/> then stands for the TV show / news organisation
/// (or the film itself), and <see cref="Episode"/> for the playable. Projections are never saved as
/// podcasts or episodes.
/// </summary>
public record PodcastEpisode(Podcast Podcast, Episode Episode)
{
    /// <summary>Null for a podcast episode; otherwise <c>TvShowEpisode</c>, <c>NewsReport</c> or <c>Film</c>.</summary>
    public string? ContentKind { get; init; }

    public bool IsCataloguePlayable => ContentKind is
        nameof(ContentKinds.ContentKind.TvShowEpisode) or
        nameof(ContentKinds.ContentKind.NewsReport) or
        nameof(ContentKinds.ContentKind.Film);

    public bool IsFilm => ContentKind == nameof(ContentKinds.ContentKind.Film);
}

using RedditPodcastPoster.Models.Episodes;

namespace RedditPodcastPoster.UrlShortening.Extensions;

/// <summary>
/// Public site link for a TV episode, news report or film social post.
/// The podcast-shaped KV short link (<c>{podcastSafeName}/{episodeId}</c>) does not fit these kinds,
/// so posts link straight to the kind route the website serves
/// (<c>/tv/:slug/:query</c>, <c>/news/:slug/:query</c>, <c>/film/:slug/:query</c>).
/// Slug matches the website <c>cataloguePlayableLink</c>: the film's own name for Film,
/// the parent name for TV and news.
/// </summary>
public static class CataloguePlayableSiteLink
{
    public const string SiteBase = "https://cultpodcasts.com";

    public static Uri For(PodcastEpisode item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (!item.IsCataloguePlayable)
        {
            throw new ArgumentException("Podcast episodes use the shortener, not a catalogue site link.", nameof(item));
        }

        var slug = item.IsFilm ? item.Episode.Title : item.Podcast.Name;
        return new Uri(SiteBase + CatalogueShortId.PlayablePath(slug.Trim(), item.Episode.Id, item.ContentKind));
    }
}

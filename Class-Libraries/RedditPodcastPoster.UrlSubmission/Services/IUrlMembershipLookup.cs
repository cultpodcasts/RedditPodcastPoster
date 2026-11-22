using RedditPodcastPoster.UrlSubmission.Models;

namespace RedditPodcastPoster.UrlSubmission.Services;

public interface IUrlMembershipLookup
{
    Task<UrlMembershipLookupResult> Lookup(Uri url, CancellationToken cancellationToken);

    /// <summary>
    /// Film, TV episode, or news report that already stores this YouTube, Spotify, or Apple URL, including a film
    /// that holds it as a trailer (<c>trailers[].url</c>).
    /// Not gated by submit content types: the indexer must not re-add a moved item while submit
    /// still persists podcasts. Other hosts return null without querying catalogue containers.
    /// </summary>
    Task<CatalogueServiceUrlOwner?> FindCatalogueServiceUrlOwner(Uri url, CancellationToken cancellationToken);

    /// <summary>
    /// The first catalogue item that already holds this YouTube id as a primary, an alternate, or a film
    /// trailer, excluding <paramref name="exceptIds"/> (RPP #1411 curator uniqueness). Exclusions are
    /// applied in the queries so a later holder is not missed when the first match is the source episode
    /// or the target itself. Podcast-episode primaries are included (unlike
    /// <see cref="FindCatalogueServiceUrlOwner"/>, which only treats episode <c>additionalYouTube</c> as
    /// catalogue-owned so submit/discovery/indexer keep their existing per-podcast merge).
    /// </summary>
    Task<CatalogueServiceUrlOwner?> FindYouTubeHolderExcluding(
        string youTubeId,
        IReadOnlyCollection<Guid> exceptIds,
        CancellationToken cancellationToken);
}

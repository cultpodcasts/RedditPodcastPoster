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
}

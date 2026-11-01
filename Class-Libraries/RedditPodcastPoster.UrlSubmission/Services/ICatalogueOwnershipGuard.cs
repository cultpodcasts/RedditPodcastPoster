using RedditPodcastPoster.UrlSubmission.Models;

namespace RedditPodcastPoster.UrlSubmission.Services;

/// <summary>
/// Finds the film, TV episode or news report that already stores a YouTube, Spotify or Apple URL, so a submit
/// (toolbar or discovery) does not re-create a podcast episode for it (#1089).
/// </summary>
public interface ICatalogueOwnershipGuard
{
    /// <summary>The catalogue owner of a podcast-platform URL; null for an unowned URL or any other URL.</summary>
    Task<CatalogueServiceUrlOwner?> FindOwner(Uri url, CancellationToken cancellationToken);
}

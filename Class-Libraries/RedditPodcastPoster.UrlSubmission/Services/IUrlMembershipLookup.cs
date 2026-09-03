using RedditPodcastPoster.UrlSubmission.Models;

namespace RedditPodcastPoster.UrlSubmission.Services;

public interface IUrlMembershipLookup
{
    Task<UrlMembershipLookupResult> Lookup(Uri url, CancellationToken cancellationToken);
}

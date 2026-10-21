using RedditPodcastPoster.UrlSubmission.Models;
using RedditPodcastPoster.UrlSubmission.Services;

namespace Api.Services.SubmitUrl;

public interface ISubmitUrlLookupService
{
    Task<UrlMembershipLookupResult> LookupAsync(Uri url, CancellationToken cancellationToken);
}

public class SubmitUrlLookupService(IUrlMembershipLookup urlMembershipLookup) : ISubmitUrlLookupService
{
    public Task<UrlMembershipLookupResult> LookupAsync(Uri url, CancellationToken cancellationToken) =>
        urlMembershipLookup.Lookup(url, cancellationToken);
}

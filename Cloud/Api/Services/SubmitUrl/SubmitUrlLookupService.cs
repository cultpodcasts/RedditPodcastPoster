using Api.Dtos;
using RedditPodcastPoster.UrlSubmission.Services;

namespace Api.Services.SubmitUrl;

public interface ISubmitUrlLookupService
{
    Task<SubmitUrlLookupResponse> LookupAsync(Uri url, CancellationToken cancellationToken);
}

public class SubmitUrlLookupService(IUrlMembershipLookup urlMembershipLookup) : ISubmitUrlLookupService
{
    public async Task<SubmitUrlLookupResponse> LookupAsync(Uri url, CancellationToken cancellationToken)
    {
        var result = await urlMembershipLookup.Lookup(url, cancellationToken);
        return SubmitUrlLookupResponse.From(result);
    }
}

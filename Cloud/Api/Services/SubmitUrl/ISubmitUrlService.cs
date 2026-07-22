using Api.Models;
using RedditPodcastPoster.UrlSubmission.Models;

namespace Api.Services.SubmitUrl;

public interface ISubmitUrlService
{
    Task<SubmitUrlResult> SubmitAsync(SubmitUrlRequest submitUrlModel, CancellationToken cancellationToken);
}

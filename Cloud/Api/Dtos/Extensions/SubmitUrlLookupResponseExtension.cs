using RedditPodcastPoster.UrlSubmission.Models;

namespace Api.Dtos.Extensions;

public static class SubmitUrlLookupResponseExtension
{
    public static SubmitUrlLookupResponse ToDto(this UrlMembershipLookupResult result) =>
        new()
        {
            Known = result.Known,
            Kind = result.Kind,
            PodcastId = result.PodcastId,
            PodcastName = result.PodcastName,
            Ambiguous = result.Ambiguous,
            PodcastIds = result.PodcastIds,
            Service = result.Service,
            ContentKind = result.ContentKind,
            ParentName = result.ParentName
        };
}

using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.PodcastServices.Abstractions.Streaming;
using RedditPodcastPoster.UrlSubmission.Services;

namespace Api.Dtos.Extensions;

public static class SubmitUrlPrepareResponseExtension
{
    public static SubmitUrlPrepareResponse ToDto(
        this NonPodcastServiceItemMetaData meta,
        StreamingService service)
    {
        return new SubmitUrlPrepareResponse
        {
            Service = StreamingServiceWire.ToKey(service),
            PodcastName = NonPodcastShowNameResolver.TrySeriesName(meta.ShowName, meta.Publisher, service),
            Title = meta.Title,
            Description = meta.Description,
            Duration = meta.Duration,
            Release = meta.Release,
            Image = meta.Image,
            Explicit = meta.Explicit,
            Publisher = meta.Publisher,
            ShowName = meta.ShowName
        };
    }
}

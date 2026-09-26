using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;

namespace RedditPodcastPoster.UrlSubmission.Services;

public static class PodcastNameAttachLookup
{
    public static Task<IReadOnlyList<Podcast>> FindByName(
        IPodcastRepository podcastRepository,
        string podcastName,
        CancellationToken cancellationToken = default) =>
        PublisherNameAttachLookup.FindByName(podcastRepository, podcastName, cancellationToken);
}

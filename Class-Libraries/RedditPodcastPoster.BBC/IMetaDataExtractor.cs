using RedditPodcastPoster.PodcastServices.Abstractions;

namespace RedditPodcastPoster.BBC;

public interface IMetaDataExtractor
{
    Task<NonPodcastServiceItemMetaData> Extract(Uri url, HttpResponseMessage pageResponse);
}

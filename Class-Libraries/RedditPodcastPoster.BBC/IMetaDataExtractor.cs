using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;

namespace RedditPodcastPoster.BBC;

public interface IMetaDataExtractor
{
    Task<NonPodcastServiceItemMetaData> Extract(Uri url, HttpResponseMessage pageResponse);
}

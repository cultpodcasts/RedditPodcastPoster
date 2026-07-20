using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;

namespace RedditPodcastPoster.InternetArchive;

public interface IMetaDataExtractor
{
    Task<NonPodcastServiceItemMetaData> Extract(Uri url, HttpResponseMessage pageResponse);
}

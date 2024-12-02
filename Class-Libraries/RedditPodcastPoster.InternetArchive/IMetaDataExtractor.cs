using RedditPodcastPoster.PodcastServices.Abstractions;

namespace RedditPodcastPoster.InternetArchive;

public interface IMetaDataExtractor
{
    Task<NonPodcastServiceItemMetaData> Extract(Uri url, HttpResponseMessage pageResponse);
}
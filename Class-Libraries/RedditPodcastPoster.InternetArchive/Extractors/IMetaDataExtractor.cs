using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;

namespace RedditPodcastPoster.InternetArchive.Extractors;

public interface IMetaDataExtractor
{
    Task<NonPodcastServiceItemMetaData> Extract(Uri url, HttpResponseMessage pageResponse);
}

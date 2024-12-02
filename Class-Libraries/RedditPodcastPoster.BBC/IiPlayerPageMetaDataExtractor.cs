using RedditPodcastPoster.PodcastServices.Abstractions;

namespace RedditPodcastPoster.BBC;

// ReSharper disable once InconsistentNaming
public interface IiPlayerPageMetaDataExtractor
{
    Task<NonPodcastServiceItemMetaData> GetMetaData(Uri url);
}
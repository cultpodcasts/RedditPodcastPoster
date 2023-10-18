using RedditPodcastPoster.Models;
using RedditPodcastPoster.PodcastServices.Abstractions;

namespace RedditPodcastPoster.UrlSubmission.Categorisation;

public interface IUrlCategoriser
{
    Task<CategorisedItem> Categorise(
        IList<Podcast> podcasts, 
        Uri url, IndexingContext indexingContext,
        bool searchForPodcast, 
        bool matchOtherServices);
}
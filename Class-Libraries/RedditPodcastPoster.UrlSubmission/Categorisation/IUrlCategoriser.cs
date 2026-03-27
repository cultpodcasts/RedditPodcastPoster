using RedditPodcastPoster.Models;
using RedditPodcastPoster.PodcastServices.Abstractions;

namespace RedditPodcastPoster.UrlSubmission.Categorisation;

public interface IUrlCategoriser
{
    Task<CategorisedItem> Categorise(
        Podcast? podcast,
        Uri url, 
        IndexingContext indexingContext,
        bool matchOtherServices);
}
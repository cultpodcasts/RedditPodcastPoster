using RedditPodcastPoster.Models;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;

namespace RedditPodcastPoster.UrlSubmission.Categorisation;

public interface IUrlCategoriser
{
    Task<CategorisedItem> Categorise(
        Podcast? podcast,
        Uri url, 
        IndexingContext indexingContext,
        bool matchOtherServices);
}

using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Common.UrlCategorisation;

public interface IUrlCategoriser
{
    Task<CategorisedItem> Categorise(IList<Podcast> podcasts, Uri url, IndexingContext indexingContext);
}
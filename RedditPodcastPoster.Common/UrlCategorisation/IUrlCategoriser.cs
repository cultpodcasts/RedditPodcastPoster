namespace RedditPodcastPoster.Common.UrlCategorisation;

public interface IUrlCategoriser
{
    Task<CategorisedItem> Categorise(Uri url, bool bypassYouTube);
}
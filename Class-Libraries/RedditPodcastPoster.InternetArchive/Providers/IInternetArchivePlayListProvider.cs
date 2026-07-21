using HtmlAgilityPack;
using RedditPodcastPoster.InternetArchive.Models;

namespace RedditPodcastPoster.InternetArchive.Providers;

public interface IInternetArchivePlayListProvider
{
    IEnumerable<PlayListItem> GetPlayList(HtmlDocument document);
}

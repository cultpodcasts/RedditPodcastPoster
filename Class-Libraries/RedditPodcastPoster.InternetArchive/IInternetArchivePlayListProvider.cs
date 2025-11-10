using HtmlAgilityPack;
using RedditPodcastPoster.InternetArchive.Models;

namespace RedditPodcastPoster.InternetArchive;

public interface IInternetArchivePlayListProvider
{
    IEnumerable<PlayListItem> GetPlayList(HtmlDocument document);
}
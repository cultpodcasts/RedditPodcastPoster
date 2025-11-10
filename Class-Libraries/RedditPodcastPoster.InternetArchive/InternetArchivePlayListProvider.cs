using System.Text.Json;
using HtmlAgilityPack;
using RedditPodcastPoster.InternetArchive.Models;

namespace RedditPodcastPoster.InternetArchive;

public class InternetArchivePlayListProvider : IInternetArchivePlayListProvider
{
    public IEnumerable<PlayListItem> GetPlayList(HtmlDocument document)
    {
        var playListNodes = document.DocumentNode.SelectNodes("//play-av");
        var items = Enumerable.Empty<PlayListItem>();
        if (playListNodes.Any())
        {
            var firstPlayListNode = playListNodes.First();
            var playListAttribute = firstPlayListNode.Attributes["playlist"];
            if (playListAttribute != null)
            {
                var playlistJson = playListAttribute.Value;
                items = JsonSerializer.Deserialize<PlayListItem[]>(playlistJson);
            }
        }

        return items ?? Enumerable.Empty<PlayListItem>();
    }
}
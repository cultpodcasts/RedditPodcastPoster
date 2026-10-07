using System.Text.Json;
using HtmlAgilityPack;
using RedditPodcastPoster.InternetArchive.Models;

namespace RedditPodcastPoster.InternetArchive.Providers;

public class InternetArchivePlayListProvider : IInternetArchivePlayListProvider
{
    public IEnumerable<PlayListItem> GetPlayList(HtmlDocument document)
    {
        var playListNodes = document.DocumentNode.SelectNodes("//play-av");
        if (playListNodes is not { Count: > 0 })
        {
            return [];
        }

        var playlistJson = playListNodes[0].Attributes["playlist"]?.Value;
        if (string.IsNullOrWhiteSpace(playlistJson))
        {
            return [];
        }

        return JsonSerializer.Deserialize<PlayListItem[]>(playlistJson) ?? [];
    }
}

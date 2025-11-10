using System.Text.Json;
using System.Web;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.InternetArchive.Models;
using RedditPodcastPoster.PodcastServices.Abstractions;

namespace RedditPodcastPoster.InternetArchive;

public class MetaDataExtractor(
    ILogger<MetaDataExtractor> logger
) : IMetaDataExtractor
{
    public async Task<NonPodcastServiceItemMetaData> Extract(Uri url, HttpResponseMessage pageResponse)
    {
        var document = new HtmlDocument();
        document.Load(await pageResponse.Content.ReadAsStreamAsync());
        var titleNodes = document.DocumentNode.SelectNodes("/html/head/title");
        if (!titleNodes.Any())
        {
            throw new InvalidOperationException($"Cannot extract title from '{url}'.");
        }

        var titleNode = titleNodes.First();
        var title = titleNode.InnerText;
        Uri? image = null;
        TimeSpan? duration = null;
        string? description = null;


        var playListNodes = document.DocumentNode.SelectNodes("//play-av");
        if (playListNodes.Any())
        {
            var firstPlayListNode = playListNodes.First();
            var playListAttribute = firstPlayListNode.Attributes["playlist"];
            if (playListAttribute != null)
            {
                var playlistJson = playListAttribute.Value;
                var items = JsonSerializer.Deserialize<PlayListItem[]>(playlistJson);
                PlayListItem? item = null;
                if (items.Length > 1)
                {
                    item = items.SingleOrDefault(x => HttpUtility.UrlDecode(url.ToString()).EndsWith(x.Orig));
                }
                else if (items.Length == 1)
                {
                    item = items.Single();
                    var descriptNode = document.DocumentNode.SelectSingleNode("//div[@id='descript']");
                    if (descriptNode != null)
                    {
                        description = descriptNode.InnerText.Trim();
                    }
                }

                if (item != null)
                {
                    image = new Uri(url, item.Image);
                    duration = item.Duration;
                }
            }
        }


        return new NonPodcastServiceItemMetaData(title, description ?? string.Empty, duration, Image: image);
    }
}
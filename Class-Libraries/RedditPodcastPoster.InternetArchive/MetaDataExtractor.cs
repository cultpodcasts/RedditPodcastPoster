using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
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


        return new NonPodcastServiceItemMetaData(title, string.Empty);
    }
}
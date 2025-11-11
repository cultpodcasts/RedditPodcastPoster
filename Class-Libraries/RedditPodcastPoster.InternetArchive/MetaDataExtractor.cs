using System.Globalization;
using System.Web;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.InternetArchive.Models;
using RedditPodcastPoster.PodcastServices.Abstractions;

namespace RedditPodcastPoster.InternetArchive;

public class MetaDataExtractor(
    IInternetArchivePlayListProvider internetArchivePlayListProvider,
    ILogger<MetaDataExtractor> logger
) : IMetaDataExtractor
{
    public async Task<NonPodcastServiceItemMetaData> Extract(Uri url, HttpResponseMessage pageResponse)
    {
        var document = new HtmlDocument();
        document.Load(await pageResponse.Content.ReadAsStreamAsync());
        var titleNode = document.DocumentNode.SelectSingleNode("//span[@itemprop='name']");

        var title = titleNode.InnerText;
        Uri? image = null;
        TimeSpan? duration = null;
        string? description = null;
        DateTime? release = null;
        string? publisher = null;

        var items = internetArchivePlayListProvider.GetPlayList(document).ToList();

        if (items.Any())
        {
            PlayListItem? item = null;
            if (items.Count() == 1)
            {
                item = items.Single();
                var descriptNode = document.DocumentNode.SelectSingleNode("//div[@id='descript']");
                if (descriptNode != null)
                {
                    description = descriptNode.InnerText.Trim();
                }

                var releaseNode = document.DocumentNode.SelectSingleNode("//span[@itemprop='uploadDate']");
                if (releaseNode != null)
                {
                    try
                    {
                        release = DateTime.ParseExact(releaseNode.InnerText.Trim(), "yyyy-MM-dd HH:mm:ss",
                            CultureInfo.InvariantCulture);
                    }
                    catch (Exception e)
                    {
                        logger.LogError(e, "Unable to parse '{releaseDate}'", releaseNode.InnerText.Trim());
                    }
                }

                var publisherNode = document.DocumentNode.SelectSingleNode(
                    "//section[contains(@class,'item-upload-info')]/p/a[contains(@class,'item-upload-info__uploader-name')]");
                if (publisherNode != null)
                {
                    publisher = publisherNode.InnerText.Trim();
                }
            }
            else
            {
                item = items.SingleOrDefault(x => HttpUtility.UrlDecode(url.ToString()).EndsWith(x.Orig));
                title = item?.Title;
            }

            if (item.Image != null)
            {
                image = new Uri(url, item.Image);
            }

            duration = item.Duration;
        }

        return new NonPodcastServiceItemMetaData(title, description ?? string.Empty, duration, release, image,
            Publisher: publisher);
    }
}
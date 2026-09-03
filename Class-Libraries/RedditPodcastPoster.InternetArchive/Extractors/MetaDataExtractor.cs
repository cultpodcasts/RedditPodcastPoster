using System.Globalization;
using System.Web;
using Microsoft.Extensions.Logging;
using HtmlAgilityPack;
using RedditPodcastPoster.InternetArchive.Models;
using RedditPodcastPoster.InternetArchive.Providers;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;

namespace RedditPodcastPoster.InternetArchive.Extractors;

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

        var pageTitle = titleNode?.InnerText.Trim();
        var title = pageTitle;
        Uri? image = null;
        TimeSpan? duration = null;
        string? description = null;
        DateTime? release = null;
        string? publisher = null;
        string? showName = null;

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
                item = items.SingleOrDefault(x => HttpUtility.UrlDecode(url.ToString()).EndsWith(x.Orig)) ??
                       items.First();

                title = item?.Title.Trim();
                showName = DistinctFromEpisodeTitle(title, pageTitle);
            }

            if (item == null)
            {
                return new NonPodcastServiceItemMetaData(title ?? string.Empty, description ?? string.Empty, duration,
                    release, image, Publisher: publisher, ShowName: showName);
            }

            if (item.Image != null)
            {
                image = new Uri(url, item.Image);
            }

            duration = item.Duration;
        }

        return new NonPodcastServiceItemMetaData(title ?? string.Empty, description ?? string.Empty, duration, release,
            image, Publisher: publisher, ShowName: showName);
    }

    private static string? DistinctFromEpisodeTitle(string? episodeTitle, string? candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return null;
        }

        var series = candidate.Trim();
        if (string.Equals(series, episodeTitle?.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return series;
    }
}

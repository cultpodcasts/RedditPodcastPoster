using Microsoft.Extensions.Logging;
using RedditPodcastPoster.BBC;
using RedditPodcastPoster.InternetArchive;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.PodcastServices.Abstractions;

namespace RedditPodcastPoster.PodcastServices;

public class StreamingServiceMetaDataHandler(
    IiPlayerPageMetaDataExtractor bbcMetaDataExtractor,
    IInternetArchivePageMetaDataExtractor internetArchivePageMetaDataExtractor,
#pragma warning disable CS9113 // Parameter is unread.
    ILogger<StreamingServiceMetaDataHandler> logger
#pragma warning restore CS9113 // Parameter is unread.
) : IStreamingServiceMetaDataHandler
{
    public async Task<ResolvedNonPodcastServiceItem> ResolveServiceItem(
        Podcast? podcast,
        Uri url)
    {
        NonPodcastService service;
        NonPodcastServiceItemMetaData metaData;
        string publisher;
        if (InternetArchiveUrlMatcher.IsInternetArchiveUrl(url))
        {
            metaData = await internetArchivePageMetaDataExtractor.GetMetaData(url);
            publisher = "Internet Archive";
            service = NonPodcastService.InternetArchive;
        }
        else if (BBCUrlMatcher.IsBBCUrl(url))
        {
            metaData = await bbcMetaDataExtractor.GetMetaData(url);
            publisher = "BBC";
            service = NonPodcastService.BBC;
        }
        else
        {
            throw new InvalidOperationException($"Url $'{url}' cannot be handled");
        }

        return new ResolvedNonPodcastServiceItem(
            service,
            podcast,
            null,
            url,
            metaData.Title,
            metaData.Description,
            publisher,
            metaData.Image,
            metaData.Release,
            metaData.Duration,
            metaData.Explicit);
    }
}
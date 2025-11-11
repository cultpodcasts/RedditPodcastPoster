using Microsoft.Extensions.Logging;
using RedditPodcastPoster.BBC;
using RedditPodcastPoster.InternetArchive;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.PodcastServices.Abstractions;

namespace RedditPodcastPoster.PodcastServices;

public class StreamingServiceMetaDataHandler(
    IBBCPageMetaDataExtractor bbcPageMetaDataExtractor,
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
        Episode? matchingEpisode;

        if (InternetArchiveUrlMatcher.IsInternetArchiveUrl(url))
        {
            metaData = await internetArchivePageMetaDataExtractor.GetMetaData(url);
            service = NonPodcastService.InternetArchive;
            if (podcast?.Episodes.Count(x => x.Urls.InternetArchive == url) > 1)
            {
                logger.LogError(
                    "Multiple episodes of podcast with podcast-id {podcastId} with internet-archive url '{url}'.",
                    podcast.Id, url);
            }

            matchingEpisode = podcast?.Episodes.FirstOrDefault(x => x.Urls.InternetArchive == url);
        }
        else if (BBCUrlMatcher.IsBBCUrl(url))
        {
            metaData = await bbcPageMetaDataExtractor.GetMetaData(url);
            service = NonPodcastService.BBC;
            if (podcast?.Episodes.Count(x => x.Urls.BBC == url) > 1)
            {
                logger.LogError("Multiple episodes of podcast with podcast-id {podcastId} with bbc url '{url}'.",
                    podcast.Id, url);
            }

            matchingEpisode = podcast?.Episodes.FirstOrDefault(x => x.Urls.BBC == url);
        }
        else
        {
            throw new InvalidOperationException($"Url $'{url}' cannot be handled");
        }

        return new ResolvedNonPodcastServiceItem(
            service,
            podcast,
            matchingEpisode,
            url,
            metaData.Title,
            metaData.Description,
            metaData.Publisher,
            metaData.Image,
            metaData.Release,
            metaData.Duration,
            metaData.Explicit);
    }
}
using System.Net;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.PodcastServices.Abstractions;

namespace RedditPodcastPoster.BBC;

// ReSharper disable once InconsistentNaming
public class BBCPageMetaDataExtractor(
    IBBCHttpClient httpClient,
    IiPlayerPageMetaDataExtractor iPlayerPageMetaDataExtractor,
    ISoundsPageMetaDataExtractor soundsPageMetaDataExtractor,
    ILogger<BBCPageMetaDataExtractor> logger
) : IBBCPageMetaDataExtractor
{
    public async Task<NonPodcastServiceItemMetaData> GetMetaData(Uri url)
    {
        var pageResponse = await httpClient.GetAsync(url);
        if (pageResponse.StatusCode != HttpStatusCode.OK)
        {
            throw new NonPodcastServiceMetaDataExtractionException(url, pageResponse.StatusCode);
        }
        NonPodcastServiceItemMetaData metaData;
        if (ServiceMatcher.IsSounds(url))
        {
            logger.LogInformation("For url '{url}' using extractor of type '{extractorType}'.", url, nameof(ISoundsPageMetaDataExtractor));
            metaData = await soundsPageMetaDataExtractor.Extract(url, pageResponse);
        }
        else if (ServiceMatcher.IsIplayer(url))
        {
            logger.LogInformation("For url '{url}' using extractor of type '{extractorType}'.", url, nameof(IiPlayerPageMetaDataExtractor));
            metaData = await iPlayerPageMetaDataExtractor.Extract(url, pageResponse);
        }
        else
        {
            throw new InvalidOperationException($"Unknown bbc-service for url '{url}'.");
        }
        return metaData;
    }
}
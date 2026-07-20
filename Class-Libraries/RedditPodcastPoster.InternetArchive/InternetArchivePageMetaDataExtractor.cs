using System.Net;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.PodcastServices.Abstractions.Exceptions;

namespace RedditPodcastPoster.InternetArchive;

// ReSharper disable once InconsistentNaming
public class InternetArchivePageMetaDataExtractor(
    IInternetArchiveHttpClient httpClient,
    IMetaDataExtractor metaDataExtractor
) : IInternetArchivePageMetaDataExtractor
{
    public async Task<NonPodcastServiceItemMetaData> GetMetaData(Uri url)
    {
        var pageResponse = await httpClient.GetAsync(url);
        if (pageResponse.StatusCode != HttpStatusCode.OK)
        {
            throw new NonPodcastServiceMetaDataExtractionException(url, pageResponse.StatusCode);
        }

        var metaData = await metaDataExtractor.Extract(url, pageResponse);

        return metaData;
    }
}

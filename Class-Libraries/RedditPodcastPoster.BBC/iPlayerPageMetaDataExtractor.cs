using System.Net;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.PodcastServices.Abstractions;

namespace RedditPodcastPoster.BBC;

// ReSharper disable once InconsistentNaming
public class iPlayerPageMetaDataExtractor(
    IBBCHttpClient httpClient,
    IMetaDataExtractor metaDataExtractor,
    ILogger<iPlayerPageMetaDataExtractor> logger
) : IiPlayerPageMetaDataExtractor
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
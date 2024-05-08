using Microsoft.Extensions.Logging;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.UrlSubmission;

namespace SubmitUrl;

public class SubmitUrlProcessor(
    IUrlSubmitter urlSubmitter,
    ILogger<SubmitUrlProcessor> logger)
{
    public async Task Process(SubmitUrlRequest request)
    {
        var indexOptions = new IndexingContext {SkipPodcastDiscovery = false};
        if (request.AllowExpensiveQueries)
        {
            indexOptions = indexOptions with
            {
                SkipExpensiveYouTubeQueries = false,
                SkipExpensiveSpotifyQueries = false
            };
        }

        var searchForPodcast = true;

        if (!request.SubmitUrlsInFile)
        {
            await urlSubmitter.Submit(
                new Uri(request.UrlOrFile, UriKind.Absolute), 
                indexOptions, 
                searchForPodcast,
                request.MatchOtherServices, 
                request.PodcastId,
                new SubmitOptions(!request.DryRun));
        }
        else
        {
            var urls = await File.ReadAllLinesAsync(request.UrlOrFile);
            foreach (var url in urls)
            {
                logger.LogInformation($"Ingesting '{url}'.");
                await urlSubmitter.Submit(
                    new Uri(url, UriKind.Absolute), 
                    indexOptions, 
                    searchForPodcast,
                    request.MatchOtherServices, 
                    request.PodcastId,
                    new SubmitOptions(!request.DryRun));
            }
        }
    }
}
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.Search;
using RedditPodcastPoster.UrlSubmission;

namespace SubmitUrl;

public class SubmitUrlProcessor(
    IUrlSubmitter urlSubmitter,
    ISearchIndexerService searchIndexerService,
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

        if (!request.SubmitUrlsInFile)
        {
            var result = await urlSubmitter.Submit(
                new Uri(request.UrlOrFile, UriKind.Absolute),
                indexOptions,
                new SubmitOptions(request.PodcastId, request.MatchOtherServices, !request.DryRun));
            logger.LogInformation(result.ToString());
        }
        else
        {
            var urls = await File.ReadAllLinesAsync(request.UrlOrFile);
            foreach (var url in urls)
            {
                logger.LogInformation($"Ingesting '{url}'.");
                var result = await urlSubmitter.Submit(
                    new Uri(url, UriKind.Absolute),
                    indexOptions,
                    new SubmitOptions(request.PodcastId, request.MatchOtherServices, !request.DryRun));
                logger.LogInformation(result.ToString());
            }
        }

        if (!request.NoIndex)
        {
            await searchIndexerService.RunIndexer();
        }
    }
}
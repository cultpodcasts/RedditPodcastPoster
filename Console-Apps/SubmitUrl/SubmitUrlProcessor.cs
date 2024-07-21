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
            await urlSubmitter.Submit(
                new Uri(request.UrlOrFile, UriKind.Absolute),
                indexOptions,
                new SubmitOptions(request.PodcastId, request.MatchOtherServices, !request.DryRun));
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
                    new SubmitOptions(request.PodcastId, request.MatchOtherServices, !request.DryRun));
            }
        }

        if (!request.NoIndex)
        {
            await searchIndexerService.RunIndexer();
        }
    }
}
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence.Abstractions;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.UrlSubmission;

namespace SubmitUrl;

public class SubmitUrlProcessor(
    IPodcastRepository podcastRepository,
    IUrlSubmitter urlSubmitter,
    ILogger<SubmitUrlProcessor> logger)
    : ISubmitUrlProcessor
{
    public async Task Process(SubmitUrlRequest request)
    {
        var indexOptions = new IndexingContext {SkipPodcastDiscovery = false};
        if (request.AllowExpensiveQueries)
        {
            indexOptions.SkipExpensiveYouTubeQueries = false;
            indexOptions.SkipExpensiveSpotifyQueries = false;
        }

        List<Podcast> podcasts;
        var searchForPodcast = true;
        if (request.PodcastId != null)
        {
            searchForPodcast = false;
            var podcast = await podcastRepository.GetPodcast(request.PodcastId.Value);
            if (podcast != null)
            {
                podcasts = new List<Podcast> {podcast};
            }
            else
            {
                logger.LogError($"No podcast found with id '{request.PodcastId}'.");
                return;
            }
        }
        else
        {
            podcasts = await podcastRepository.GetAll().ToListAsync();
        }

        if (!request.SubmitUrlsInFile)
        {
            await urlSubmitter.Submit(podcasts, new Uri(request.UrlOrFile, UriKind.Absolute), indexOptions,
                searchForPodcast, request.MatchOtherServices);
        }
        else
        {
            var urls = await File.ReadAllLinesAsync(request.UrlOrFile);
            foreach (var url in urls)
            {
                logger.LogInformation($"Ingesting '{url}'.");
                await urlSubmitter.Submit(
                    podcasts,
                    new Uri(url, UriKind.Absolute),
                    indexOptions,
                    searchForPodcast,
                    request.MatchOtherServices);
            }
        }
    }
}
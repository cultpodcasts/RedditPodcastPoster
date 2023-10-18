using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Common;
using RedditPodcastPoster.Common.UrlSubmission;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence;

namespace SubmitUrl;

public class SubmitUrlProcessor : ISubmitUrlProcessor
{
    private readonly ILogger<SubmitUrlProcessor> _logger;
    private readonly IPodcastRepository _podcastRepository;
    private readonly IUrlSubmitter _urlSubmitter;

    public SubmitUrlProcessor(
        IPodcastRepository podcastRepository,
        IUrlSubmitter urlSubmitter,
        ILogger<SubmitUrlProcessor> logger)
    {
        _podcastRepository = podcastRepository;
        _urlSubmitter = urlSubmitter;
        _logger = logger;
    }


    public async Task Process(SubmitUrlRequest request)
    {
        var indexOptions = new IndexingContext {SkipPodcastDiscovery = false};
        if (request.AllowExpensiveQueries)
        {
            indexOptions.SkipExpensiveQueries = false;
        }

        List<Podcast> podcasts;
        var searchForPodcast = true;
        if (request.PodcastId != null)
        {
            searchForPodcast = false;
            var podcast = await _podcastRepository.GetPodcast(request.PodcastId.Value);
            if (podcast != null)
            {
                podcasts = new List<Podcast> {podcast};
            }
            else
            {
                _logger.LogError($"No podcast found with id '{request.PodcastId}'.");
                return;
            }
        }
        else
        {
            podcasts = await _podcastRepository.GetAll().ToListAsync();
        }

        if (!request.SubmitUrlsInFile)
        {
            await _urlSubmitter.Submit(podcasts, new Uri(request.UrlOrFile, UriKind.Absolute), indexOptions,
                searchForPodcast, request.MatchOtherServices);
        }
        else
        {
            var urls = await File.ReadAllLinesAsync(request.UrlOrFile);
            foreach (var url in urls)
            {
                _logger.LogInformation($"Ingesting '{url}'.");
                await _urlSubmitter.Submit(podcasts, new Uri(url, UriKind.Absolute), indexOptions, searchForPodcast,
                    request.MatchOtherServices);
            }
        }
    }
}
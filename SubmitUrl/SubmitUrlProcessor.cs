using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Common;
using RedditPodcastPoster.Common.Podcasts;
using RedditPodcastPoster.Common.UrlSubmission;

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
        var indexOptions = new IndexingContext();
        var podcasts = await _podcastRepository.GetAll().ToListAsync();

        if (!request.SubmitUrlsInFile)
        {
            await _urlSubmitter.Submit(podcasts, new Uri(request.UrlOrFile, UriKind.Absolute), indexOptions);
        }
        else
        {
            var urls = await File.ReadAllLinesAsync(request.UrlOrFile);
            foreach (var url in urls)
            {
                await _urlSubmitter.Submit(podcasts, new Uri(url, UriKind.Absolute), indexOptions);
            }
        }
    }
}
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Common.UrlSubmission;

namespace SubmitUrl;

public class SubmitUrlProcessor : ISubmitUrlProcessor
{
    private readonly ILogger<SubmitUrlProcessor> _logger;
    private readonly IUrlSubmitter _urlSubmitter;

    public SubmitUrlProcessor(IUrlSubmitter urlSubmitter, ILogger<SubmitUrlProcessor> logger)
    {
        _urlSubmitter = urlSubmitter;
        _logger = logger;
    }


    public async Task Process(SubmitUrlRequest request)
    {
        if (!request.SubmitUrlsInFIle)
        {
            await _urlSubmitter.Submit(new Uri(request.UrlOrFile, UriKind.Absolute), request.SkipYouTubeUrlResolving);
        }
        else
        {
            var urls = await File.ReadAllLinesAsync(request.UrlOrFile);
            foreach (var url in urls)
            {
                await _urlSubmitter.Submit(new Uri(url, UriKind.Absolute), request.SkipYouTubeUrlResolving);
            }
        }
    }
}
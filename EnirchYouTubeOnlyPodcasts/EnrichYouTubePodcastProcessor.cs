using Microsoft.Extensions.Logging;

namespace EnirchYouTubeOnlyPodcasts;

public class EnrichYouTubePodcastProcessor
{
    private readonly ILogger<EnrichYouTubePodcastProcessor> _logger;

    public EnrichYouTubePodcastProcessor(ILogger<EnrichYouTubePodcastProcessor> logger)
    {
        _logger = logger;
    }

    public Task Run(Guid podcastGuid)
    {
        throw new NotImplementedException();
    }
}
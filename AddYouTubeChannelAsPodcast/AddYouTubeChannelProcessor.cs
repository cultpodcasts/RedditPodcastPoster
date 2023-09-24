using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Common.Podcasts;
using RedditPodcastPoster.Common.PodcastServices.YouTube;
using RedditPodcastPoster.Models;

namespace AddYouTubeChannelAsPodcast;

public class AddYouTubeChannelProcessor
{
    private readonly ILogger<AddYouTubeChannelProcessor> _logger;
    private readonly IYouTubeChannelResolver _youTubeChannelResolver;
    private readonly IYouTubeSearchService _youTubeSearchService;
    private readonly PodcastFactory _podcastFactory;
    private readonly IPodcastRepository _repository;

    public AddYouTubeChannelProcessor(
        IYouTubeChannelResolver youTubeChannelResolver,
        IYouTubeSearchService youTubeSearchService,
        PodcastFactory podcastFactory,
        IPodcastRepository repository,
        ILogger<AddYouTubeChannelProcessor> logger)
    {
        _youTubeChannelResolver = youTubeChannelResolver;
        _youTubeSearchService = youTubeSearchService;
        _podcastFactory = podcastFactory;
        _repository = repository;
        _logger = logger;
    }

    public async Task<bool> Run(Args request)
    {
        var match = await _youTubeChannelResolver.FindChannel(request.ChannelName,
            request.MostRecentUploadedVideoTitle);
        if (match != null)
        {
            _logger.LogInformation($"Found channel-id: {match}");
            var allPodcasts = await _repository.GetAll().ToListAsync();
            var matchingPodcast = allPodcasts.SingleOrDefault(x => x.YouTubeChannelId == match.Snippet.ChannelId);
            if (matchingPodcast != null)
            {
                _logger.LogError($"Found existing podcast with YouTube-Id '{match}' with Podcast-id '{matchingPodcast.Id}'.");
                return false;
            }

            var channel = await _youTubeSearchService.GetChannel(match.Snippet.ChannelId);

            var newPodcast = _podcastFactory.Create(match.Snippet.ChannelTitle);
            newPodcast.Publisher = channel.ContentOwnerDetails.ContentOwner;
            newPodcast.YouTubePublishingDelayTimeSpan = "1:00:00:00";
            newPodcast.YouTubeChannelId= match.Snippet.ChannelId;
            newPodcast.ReleaseAuthority = Service.YouTube;
            newPodcast.PrimaryPostService = Service.YouTube;
            await _repository.Save(newPodcast);
            return true;
        }

        _logger.LogError("Failed to match channel");
        return false;
    }
}
using iTunesSearch.Library;
using iTunesSearch.Library.Models;
using Microsoft.Extensions.Logging;

namespace RedditPodcastPoster.Common.PodcastServices.Apple;

public class ApplePodcastResolver : IApplePodcastResolver
{
    private const int PodcastSearchLimit = 200;
    private readonly iTunesSearchManager _iTunesSearchManager;
    private readonly ILogger<AppleEpisodeResolver> _logger;

    public ApplePodcastResolver(
        iTunesSearchManager iTunesSearchManager,
        ILogger<AppleEpisodeResolver> logger)
    {
        _iTunesSearchManager = iTunesSearchManager;
        _logger = logger;
    }

    public async Task<Podcast?> FindPodcast(FindApplePodcastRequest request)
    {
        Podcast? matchingPodcast = null;
        if (request.PodcastAppleId != null)
        {
            var podcastResult = await _iTunesSearchManager.GetPodcastById(request.PodcastAppleId.Value);
            matchingPodcast = podcastResult.Podcasts.FirstOrDefault();
        }

        if (matchingPodcast == null)
        {
            var items = await _iTunesSearchManager.GetPodcasts(request.PodcastName, PodcastSearchLimit);
            IEnumerable<Podcast> podcasts = items.Podcasts;
            if (podcasts.Count() > 1)
            {
                podcasts = podcasts.Where(x => x.ArtistName.ToLower().Trim() == request.PodcastPublisher.ToLower());
            }

            matchingPodcast = podcasts.SingleOrDefault(x => x.Name.ToLower() == request.PodcastName.ToLower());
        }

        return matchingPodcast;
    }
}
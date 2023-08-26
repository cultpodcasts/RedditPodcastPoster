using iTunesSearch.Library;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models;

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
    public async Task<iTunesSearch.Library.Models.Podcast?> FindPodcast(Podcast podcast)
    {
        iTunesSearch.Library.Models.Podcast? matchingPodcast = null;
        if (podcast.AppleId != null)
        {
            var podcastResult = await _iTunesSearchManager.GetPodcastById(podcast.AppleId.Value);
            matchingPodcast = podcastResult.Podcasts.FirstOrDefault();
        }

        if (matchingPodcast == null)
        {
            var items = await _iTunesSearchManager.GetPodcasts(podcast.Name, PodcastSearchLimit);
            IEnumerable<iTunesSearch.Library.Models.Podcast> podcasts = items.Podcasts;
            if (podcasts.Count() > 1)
            {
                podcasts = podcasts.Where(x => x.ArtistName.Trim() == podcast.Publisher);
            }
            matchingPodcast = podcasts.SingleOrDefault(x => x.Name == podcast.Name);
        }

        return matchingPodcast;
    }
}
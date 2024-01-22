using iTunesSearch.Library;
using iTunesSearch.Library.Models;
using Microsoft.Extensions.Logging;

namespace RedditPodcastPoster.PodcastServices.Apple;

public class ApplePodcastResolver(
    iTunesSearchManager iTunesSearchManager,
    ILogger<AppleEpisodeResolver> logger)
    : IApplePodcastResolver
{
    private const int PodcastSearchLimit = 200;

    public async Task<Podcast?> FindPodcast(FindApplePodcastRequest request)
    {
        Podcast? matchingPodcast = null;
        if (request.PodcastAppleId != null)
        {
            var podcastResult = await iTunesSearchManager.GetPodcastById(request.PodcastAppleId.Value);
            matchingPodcast = podcastResult.Podcasts.FirstOrDefault();
        }

        if (matchingPodcast == null)
        {
            var items = await iTunesSearchManager.GetPodcasts(request.PodcastName, PodcastSearchLimit);
            IEnumerable<Podcast> podcasts = items.Podcasts;
            if (podcasts.Count() > 1)
            {
                podcasts = podcasts.Where(x => x.ArtistName.ToLower().Trim() == request.PodcastPublisher.ToLower());
            }

            matchingPodcast = podcasts.FirstOrDefault(x => x.Name.ToLower() == request.PodcastName.ToLower());
        }

        return matchingPodcast;
    }
}
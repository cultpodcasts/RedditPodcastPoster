using iTunesSearch.Library;
using iTunesSearch.Library.Models;
using Microsoft.Extensions.Logging;

namespace RedditPodcastPoster.PodcastServices.Apple;

public class ApplePodcastResolver(
    iTunesSearchManager iTunesSearchManager,
#pragma warning disable CS9113 // Parameter is unread.
    ILogger<AppleEpisodeResolver> logger)
#pragma warning restore CS9113 // Parameter is unread.
    : IApplePodcastResolver
{
    private const int PodcastSearchLimit = 200;

    public async Task<Podcast?> FindPodcast(FindApplePodcastRequest request)
    {
        Podcast? matchingPodcast = null;
        if (request.PodcastAppleId != null)
        {
            try
            {
                var podcastResult = await iTunesSearchManager.GetPodcastById(request.PodcastAppleId.Value);
                matchingPodcast = podcastResult.Podcasts.FirstOrDefault();
            }
            catch (HttpRequestException ex)
            {
                logger.LogError(ex,
                    $"Error invoking {nameof(iTunesSearchManager.GetPodcastById)} with id '{request.PodcastAppleId.Value}', status-code: {ex.HttpRequestError}, http-request-error: '{ex.HttpRequestError}'.");
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    $"Error invoking {nameof(iTunesSearchManager.GetPodcastById)} with id '{request.PodcastAppleId.Value}'.");
            }
        }

        if (matchingPodcast == null)
        {
            try
            {
                var items = await iTunesSearchManager.GetPodcasts(request.PodcastName, PodcastSearchLimit);
                IEnumerable<Podcast> podcasts = items.Podcasts;
                if (podcasts.Count() > 1)
                {
                    podcasts = podcasts.Where(x => x.ArtistName.ToLower().Trim() == request.PodcastPublisher.ToLower());
                }

                matchingPodcast = podcasts.FirstOrDefault(x => x.Name.ToLower() == request.PodcastName.ToLower());
            }
            catch (HttpRequestException ex)
            {
                logger.LogError(ex,
                    $"Error invoking {nameof(iTunesSearchManager.GetPodcasts)} with podcast-name '{request.PodcastName}', status-code: {ex.HttpRequestError}, http-request-error: '{ex.HttpRequestError}'.");
            }
            catch (Exception e)
            {
                logger.LogError(e,
                    $"Error invoking {nameof(iTunesSearchManager.GetPodcasts)} with podcast-name '{request.PodcastName}'.");
            }
        }

        return matchingPodcast;
    }
}
using iTunesSearch.Library;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.PodcastServices.Abstractions;

namespace RedditPodcastPoster.PodcastServices.Apple;

public class RecentApplePodcastService(
    IRemoteClient remoteClient,
    ILogger<ApplePodcastService> logger)
    : IApplePodcastService
{
    private const int PodcastEpisodeSearchLimit = 10;
    private const string Country = "US";

    public async Task<IEnumerable<AppleEpisode>?> GetEpisodes(ApplePodcastId podcastId, IndexingContext indexingContext)
    {
        var queryString = HttpUtility.ParseQueryString(string.Empty);
        queryString.Add("id", podcastId.PodcastId.ToString());
        queryString.Add("country", Country);
        queryString.Add("media", "podcast");
        queryString.Add("entity", "podcastEpisode");
        queryString.Add("limit", PodcastEpisodeSearchLimit.ToString());
        PodcastEpisodeListResult podcastEpisodeListResult;
        try
        {
            podcastEpisodeListResult = await remoteClient.InvokeGet<PodcastEpisodeListResult>(
                string.Format(
                    "https://itunes.apple.com/lookup?{0}",
                    new object[1]
                    {
                        queryString.ToString()
                    }));
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex,
                $"Failed to request episodes for podcast with apple-id '{podcastId.PodcastId}'. Reason: '{ex.Message}', Status-Code: '{ex.StatusCode}'.");
            return null;
        }

        return podcastEpisodeListResult.Episodes.Select<PodcastEpisode, AppleEpisode>(x => x.ToAppleEpisode());
    }
}
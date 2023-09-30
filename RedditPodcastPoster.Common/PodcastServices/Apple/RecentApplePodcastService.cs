using iTunesSearch.Library;
using Microsoft.Extensions.Logging;

namespace RedditPodcastPoster.Common.PodcastServices.Apple;

public class RecentApplePodcastService : IApplePodcastService
{
    private const int PodcastEpisodeSearchLimit = 10;
    private const string Country = "US";
    private readonly ILogger<ApplePodcastService> _logger;
    private readonly IRemoteClient _remoteClient;

    public RecentApplePodcastService(
        IRemoteClient remoteClient,
        ILogger<ApplePodcastService> logger)
    {
        _remoteClient = remoteClient;
        _logger = logger;
    }

    public async Task<IEnumerable<AppleEpisode>> GetEpisodes(long podcastId, DateTime? releasedSince)
    {
        var queryString = HttpUtility.ParseQueryString(string.Empty);
        queryString.Add("id", podcastId.ToString());
        queryString.Add("country", Country);
        queryString.Add("media", "podcast");
        queryString.Add("entity", "podcastEpisode");
        queryString.Add("limit", PodcastEpisodeSearchLimit.ToString());
        var podcastEpisodeListResult = await _remoteClient.InvokeGet<PodcastEpisodeListResult>(
            string.Format(
                "https://itunes.apple.com/lookup?{0}",
                new object[1]
                {
                    queryString.ToString()
                }));
        return podcastEpisodeListResult.Episodes.Select(x => x.ToAppleEpisode());
    }
}
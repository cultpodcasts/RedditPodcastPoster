using iTunesSearch.Library;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Common.PodcastServices.Spotify;
using RedditPodcastPoster.Common.Text;

namespace RedditPodcastPoster.Common.PodcastServices.Apple;

public class AppleEpisodeResolver : IAppleEpisodeResolver
{
    private const int PodcastEpisodeSearchLimit = 10;
    private const string Country = "US";
    private const int PodcastSearchLimit = 200;
    private readonly ILogger<AppleEpisodeResolver> _logger;
    private readonly IRemoteClient _remoteClient;

    public AppleEpisodeResolver(
        IRemoteClient remoteClient,
        ILogger<AppleEpisodeResolver> logger)
    {
        _remoteClient = remoteClient;
        _logger = logger;
    }

    public async Task<PodcastEpisode?> FindEpisode(FindAppleEpisodeRequest request)
    {
        PodcastEpisode? matchingEpisode = null;
        if (request.PodcastAppleId != null && request.EpisodeAppleId != null)
        {
            var episodeResult = await GetPodcastEpisodesByPodcastId(request.PodcastAppleId.Value);
            matchingEpisode = episodeResult.Episodes.FirstOrDefault(x => x.Id == request.EpisodeAppleId);
        }

        if (matchingEpisode == null)
        {
            if (request.PodcastAppleId.HasValue)
            {
                if (request.EpisodeIndex <= PodcastSearchLimit)
                {
                    var podcastEpisodes = await GetPodcastEpisodesByPodcastId(request.PodcastAppleId.Value);

                    var matchingEpisodes = podcastEpisodes.Episodes.Where(x => x.Title == request.EpisodeTitle);
                    if (!matchingEpisodes.Any() || matchingEpisodes.Count() > 1)
                    {
                        var sameDateMatches = podcastEpisodes.Episodes.Where(x =>
                            DateOnly.FromDateTime(x.Release) == DateOnly.FromDateTime(request.Released));
                        if (sameDateMatches.Count() > 1)
                        {
                            var distances =
                                sameDateMatches.OrderByDescending(x =>
                                    Levenshtein.CalculateSimilarity(request.EpisodeTitle, x.Title));
                            return distances.FirstOrDefault()!;
                        }

                        matchingEpisode = sameDateMatches.SingleOrDefault();
                    }

                    matchingEpisode ??= matchingEpisodes.FirstOrDefault();
                }
                else
                {
                    _logger.LogInformation(
                        $"Podcast '{request.PodcastName}' episode with title '{request.EpisodeTitle}' and release-date '{request.Released}' is beyond limit of Apple Lookup.");
                }
            }
            else
            {
                _logger.LogInformation(
                    $"Podcast '{request.PodcastName}' cannot be found on Apple Podcasts.");
            }
        }

        return matchingEpisode;
    }

    public async Task<PodcastEpisodeListResult> GetPodcastEpisodesByPodcastId(long podcastId)
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
        return podcastEpisodeListResult;
    }
}
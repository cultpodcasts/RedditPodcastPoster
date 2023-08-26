using iTunesSearch.Library;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Common.Text;

namespace RedditPodcastPoster.Common.PodcastServices.Apple;

public class AppleEpisodeResolver : IAppleEpisodeResolver
{
    private const int PodcastEpisodeSearchLimit = 10;
    private const string Country = "US";
    private const int PodcastSearchLimit = 200;
    private readonly ILogger<AppleEpisodeResolver> _logger;
    private readonly IRemoteClient _remoteClient;
    private readonly IApplePodcastEnricher _applePodcastEnricher;

    public AppleEpisodeResolver(
        IRemoteClient remoteClient,
        IApplePodcastEnricher applePodcastEnricher,
        ILogger<AppleEpisodeResolver> logger)
    {
        _remoteClient = remoteClient;
        _applePodcastEnricher = applePodcastEnricher;
        _logger = logger;
    }
    
    public async Task<PodcastEpisode> FindEpisode(Podcast podcast, Episode episode)
    {
        PodcastEpisode? matchingEpisode = null;
        if (podcast.AppleId != null && episode.AppleId != null)
        {
            var episodeResult = await GetPodcastEpisodesByPodcastId(podcast.AppleId.Value);
            matchingEpisode = episodeResult.Episodes.FirstOrDefault(x => x.Id == episode.AppleId);
        }
        if (matchingEpisode == null)
        {
            if (podcast.AppleId == null)
            {
                await _applePodcastEnricher.AddId(podcast);
            }

            if (podcast.AppleId.HasValue)
            {
                if (podcast.Episodes.ToList().FindIndex(x => x == episode) <= PodcastSearchLimit)
                {
                    var podcastEpisodes = await GetPodcastEpisodesByPodcastId(podcast.AppleId.Value);

                    var matchingEpisodes = podcastEpisodes.Episodes.Where(x => x.Title == episode.Title);
                    if (!matchingEpisodes.Any() || matchingEpisodes.Count() > 1)
                    {
                        var sameDateMatches = podcastEpisodes.Episodes.Where(x =>
                            DateOnly.FromDateTime(x.Release) == DateOnly.FromDateTime(episode.Release));
                        if (sameDateMatches.Count() > 1)
                        {
                            var distances =
                                sameDateMatches.OrderByDescending(x =>
                                    Levenshtein.CalculateSimilarity(episode.Title, x.Title));
                            return distances.FirstOrDefault();
                        }

                        matchingEpisode = sameDateMatches.SingleOrDefault();
                    }

                    matchingEpisode ??= matchingEpisodes.FirstOrDefault();
                }
                else
                {
                    _logger.LogInformation(
                        $"Podcast '{podcast.Name}' episode with title '{episode.Title}' and release-date '{episode.Release}' is beyond limit of Apple Lookup.");
                }
            }
            else
            {
                _logger.LogInformation(
                    $"Podcast '{podcast.Name}' cannot be found on Apple Podcasts.");
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
using iTunesSearch.Library;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Common.Text;

namespace RedditPodcastPoster.Common.PodcastServices.Apple;

public class AppleItemResolver : IAppleItemResolver
{
    private const int PodcastEpisodeSearchLimit = 10;
    private const string Country = "US";
    private const int PodcastSearchLimit = 200;
    private readonly iTunesSearchManager _iTunesSearchManager;
    private readonly ILogger<AppleItemResolver> _logger;
    private readonly IRemoteClient _remoteClient;

    public AppleItemResolver(iTunesSearchManager iTunesSearchManager, IRemoteClient remoteClient,
        ILogger<AppleItemResolver> logger)
    {
        _iTunesSearchManager = iTunesSearchManager;
        _remoteClient = remoteClient;
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
            matchingPodcast = items.Podcasts.SingleOrDefault(x => x.Name == podcast.Name);
        }

        return matchingPodcast;
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
            long? podcastAppleId = podcast.AppleId;
            if (podcastAppleId == null)
            {
                var matchingPodcast = await FindPodcast(podcast);
                if (matchingPodcast == null)
                    throw new InvalidOperationException($"Could not find matching podcast with name '{podcast.Name}'.");
                podcastAppleId = matchingPodcast.Id;

            }
            if (podcast.Episodes.ToList().FindIndex(x => x == episode) <= PodcastSearchLimit)
            {
                var podcastEpisodes = await GetPodcastEpisodesByPodcastId(podcastAppleId.Value);

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
                _logger.LogInformation($"Podcast '{podcast.Name}' episode with title '{episode.Title}' and release-date '{episode.Release}' is beyond limit of Apple Lookup.");
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
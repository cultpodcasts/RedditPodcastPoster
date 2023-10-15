using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Common.UrlCategorisation;
using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Common.PodcastServices.Spotify;

public class SpotifyUrlCategoriser : ISpotifyUrlCategoriser
{
    private static readonly Regex SpotifyId = new(@"episode/(?'episodeId'\w+)");
    private readonly ILogger<SpotifyUrlCategoriser> _logger;
    private readonly ISpotifyEpisodeResolver _spotifyEpisodeResolver;

    public SpotifyUrlCategoriser(
        ISpotifyEpisodeResolver spotifyEpisodeResolver,
        ILogger<SpotifyUrlCategoriser> logger)
    {
        _spotifyEpisodeResolver = spotifyEpisodeResolver;
        _logger = logger;
    }

    public bool IsMatch(Uri url)
    {
        return url.Host.ToLower().Contains("spotify");
    }

    public async Task<ResolvedSpotifyItem> Resolve(IList<Podcast> podcasts, Uri url, IndexingContext indexingContext)
    {
        var pair = podcasts
            .SelectMany(podcast => podcast.Episodes, (podcast, episode) => new PodcastEpisodePair(podcast, episode))
            .FirstOrDefault(pair => pair.Episode.Urls.Spotify == url);

        if (pair != null)
        {
            return new ResolvedSpotifyItem(pair);
        }

        var episodeId = SpotifyId.Match(url.ToString()).Groups["episodeId"].Value;
        if (episodeId == null)
        {
            throw new InvalidOperationException($"Unable to find spotify-id in url '{url}'.");
        }

        if (!indexingContext.SkipExpensiveQueries)
        {
            var findEpisodeResponse = await _spotifyEpisodeResolver.FindEpisode(
                FindSpotifyEpisodeRequestFactory.Create(episodeId),
                indexingContext);
            if (findEpisodeResponse.FullEpisode != null)
            {
                return new ResolvedSpotifyItem(
                    findEpisodeResponse.FullEpisode.Show.Id,
                    findEpisodeResponse.FullEpisode.Id,
                    findEpisodeResponse.FullEpisode.Show.Name,
                    findEpisodeResponse.FullEpisode.Show.Description,
                    findEpisodeResponse.FullEpisode.Show.Publisher,
                    findEpisodeResponse.FullEpisode.Name,
                    findEpisodeResponse.FullEpisode.Description,
                    findEpisodeResponse.FullEpisode.GetReleaseDate(),
                    findEpisodeResponse.FullEpisode.GetDuration(),
                    new Uri(findEpisodeResponse.FullEpisode.ExternalUrls.FirstOrDefault().Value, UriKind.Absolute),
                    findEpisodeResponse.FullEpisode.Explicit);
            }

            _logger.LogError($"Skipping finding-episode as '{nameof(indexingContext.SkipExpensiveQueries)}' is set.");
        }

        throw new InvalidOperationException($"Could not find item with spotify-id '{SpotifyId}'.");
    }

    public async Task<ResolvedSpotifyItem?> Resolve(PodcastServiceSearchCriteria criteria, Podcast? matchingPodcast,
        IndexingContext indexingContext)
    {
        var request = new FindSpotifyEpisodeRequest(
            matchingPodcast?.SpotifyId ?? string.Empty,
            (matchingPodcast?.Name ?? criteria.ShowName).Trim(),
            string.Empty,
            criteria.EpisodeTitle.Trim());
        if (!indexingContext.SkipExpensiveQueries)
        {
            var findEpisodeResponse = await _spotifyEpisodeResolver.FindEpisode(request, indexingContext);
            if (findEpisodeResponse.FullEpisode != null)
            {
                return new ResolvedSpotifyItem(
                    findEpisodeResponse.FullEpisode.Show.Id,
                    findEpisodeResponse.FullEpisode.Id,
                    findEpisodeResponse.FullEpisode.Show.Name,
                    findEpisodeResponse.FullEpisode.Show.Description,
                    findEpisodeResponse.FullEpisode.Show.Publisher,
                    findEpisodeResponse.FullEpisode.Name,
                    findEpisodeResponse.FullEpisode.Description,
                    findEpisodeResponse.FullEpisode.GetReleaseDate(),
                    findEpisodeResponse.FullEpisode.GetDuration(),
                    findEpisodeResponse.FullEpisode.GetUrl(),
                    findEpisodeResponse.FullEpisode.Explicit);
            }
        }
        else
        {
            _logger.LogError($"Skipping finding-episode as '{nameof(indexingContext.SkipExpensiveQueries)}' is set.");
        }

        _logger.LogWarning(
            $"Could not find spotify episode for show named '{criteria.ShowName}' and episode-name '{criteria.EpisodeTitle}'.");
        return null;
    }
}
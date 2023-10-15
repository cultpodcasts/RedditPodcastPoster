using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models;
using SpotifyAPI.Web;

namespace RedditPodcastPoster.Common.PodcastServices.Spotify;

public class SpotifyEpisodeProvider : ISpotifyEpisodeProvider
{
    private readonly ILogger<SpotifyEpisodeProvider> _logger;

    private readonly ISpotifyEpisodeResolver _spotifyEpisodeResolver;

    public SpotifyEpisodeProvider(
        ISpotifyEpisodeResolver spotifyEpisodeResolver,
        ILogger<SpotifyEpisodeProvider> logger)
    {
        _spotifyEpisodeResolver = spotifyEpisodeResolver;
        _logger = logger;
    }

    public async Task<GetEpisodesResponse> GetEpisodes(SpotifyPodcastId podcastId, IndexingContext indexingContext)
    {
        var getEpisodesResult =
            await _spotifyEpisodeResolver.GetEpisodes(
                new SpotifyPodcastId(podcastId.PodcastId), indexingContext);

        var expensiveQueryFound = getEpisodesResult.IsExpensiveQuery;

        IEnumerable<SimpleEpisode> episodes = getEpisodesResult.Results;
        if (indexingContext.ReleasedSince.HasValue)
        {
            episodes = episodes.Where(x => x.GetReleaseDate() >= indexingContext.ReleasedSince.Value);
        }

        return new GetEpisodesResponse(episodes.Select(x =>
            Episode.FromSpotify(
                x.Id,
                x.Name.Trim(),
                x.Description.Trim(),
                TimeSpan.FromMilliseconds(x.DurationMs),
                x.Explicit,
                x.GetReleaseDate(),
                new Uri(x.ExternalUrls.FirstOrDefault().Value, UriKind.Absolute))
        ).ToList(), expensiveQueryFound);
    }
}
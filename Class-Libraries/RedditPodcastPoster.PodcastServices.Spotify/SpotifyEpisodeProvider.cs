using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.PodcastServices.Abstractions;
using SpotifyAPI.Web;

namespace RedditPodcastPoster.PodcastServices.Spotify;

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

    public async Task<GetEpisodesResponse> GetEpisodes(GetEpisodesRequest request, IndexingContext indexingContext)
    {
        var getEpisodesResult =
            await _spotifyEpisodeResolver.GetEpisodes(request, indexingContext);

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
                new Uri(Enumerable.FirstOrDefault<KeyValuePair<string, string>>(x.ExternalUrls).Value, UriKind.Absolute))
        ).ToList(), expensiveQueryFound);
    }
}
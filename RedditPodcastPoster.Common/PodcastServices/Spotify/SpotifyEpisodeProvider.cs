using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Common.PodcastServices.Spotify;

public record SpotifyGetEpisodesRequest(string SpotifyId, DateTime? ProcessRequestReleasedSince);


public class SpotifyEpisodeProvider : ISpotifyEpisodeProvider
{
    private readonly ILogger<SpotifyEpisodeProvider> _logger;


    private readonly ISpotifyItemResolver _spotifyItemResolver;

    public SpotifyEpisodeProvider(
        ISpotifyItemResolver spotifyItemResolver,
        ILogger<SpotifyEpisodeProvider> logger)
    {
        _spotifyItemResolver = spotifyItemResolver;
        _logger = logger;
    }

    public async Task<IList<Episode>> GetEpisodes(SpotifyGetEpisodesRequest request)
    {
        var allEpisodes = await _spotifyItemResolver.GetEpisodes(request.SpotifyId);
        if (request.ProcessRequestReleasedSince.HasValue)
        {
            allEpisodes = allEpisodes.Where(x => x.GetReleaseDate() > request.ProcessRequestReleasedSince.Value);
        }

        return allEpisodes.Select(x =>
            Episode.FromSpotify(
                x.Id,
                x.Name,
                x.Description,
                TimeSpan.FromMilliseconds(x.DurationMs),
                x.Explicit,
                x.GetReleaseDate(),
                new Uri(x.ExternalUrls.FirstOrDefault().Value, UriKind.Absolute))
        ).ToList();
    }
}
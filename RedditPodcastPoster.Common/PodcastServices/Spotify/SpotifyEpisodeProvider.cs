using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Common.PodcastServices.Spotify;

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

    public async Task<IList<Episode>?> GetEpisodes(SpotifyPodcastId podcastId, IndexOptions indexOptions)
    {
        var episodes =
            await _spotifyItemResolver.GetEpisodes(
                new SpotifyPodcastId(podcastId.PodcastId), indexOptions);

        if (indexOptions.ReleasedSince.HasValue)
        {
            episodes = episodes.Where(x => x.GetReleaseDate() > indexOptions.ReleasedSince.Value);
        }

        return episodes.Select(x =>
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
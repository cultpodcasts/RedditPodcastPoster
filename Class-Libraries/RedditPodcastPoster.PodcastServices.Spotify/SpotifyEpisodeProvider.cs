using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.PodcastServices.Abstractions;
using SpotifyAPI.Web;

namespace RedditPodcastPoster.PodcastServices.Spotify;

public class SpotifyEpisodeProvider(
    ISpotifyEpisodeResolver spotifyEpisodeResolver,
    ILogger<SpotifyEpisodeProvider> logger)
    : ISpotifyEpisodeProvider
{
    public async Task<GetEpisodesResponse> GetEpisodes(GetEpisodesRequest request, IndexingContext indexingContext)
    {
        var getEpisodesResult =
            await spotifyEpisodeResolver.GetEpisodes(request, indexingContext);

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
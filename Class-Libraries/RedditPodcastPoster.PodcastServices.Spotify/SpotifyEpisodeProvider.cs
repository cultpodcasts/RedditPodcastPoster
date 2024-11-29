using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.Text;

namespace RedditPodcastPoster.PodcastServices.Spotify;

public class SpotifyEpisodeProvider(
    ISpotifyPodcastEpisodesProvider spotifyPodcastEpisodesProvider,
    IHtmlSanitiser htmlSanitiser,
#pragma warning disable CS9113 // Parameter is unread.
    ILogger<SpotifyEpisodeProvider> logger)
#pragma warning restore CS9113 // Parameter is unread.
    : ISpotifyEpisodeProvider
{
    public async Task<GetEpisodesResponse> GetEpisodes(GetEpisodesRequest request, IndexingContext indexingContext)
    {
        var getEpisodesResult = await spotifyPodcastEpisodesProvider.GetEpisodes(request, indexingContext);

        var expensiveQueryFound = getEpisodesResult.ExpensiveQueryFound;

        var episodes = getEpisodesResult.Episodes;
        if (indexingContext.ReleasedSince.HasValue)
        {
            episodes = episodes.Where(x => x.GetReleaseDate() >= indexingContext.ReleasedSince.Value);
        }

        return new GetEpisodesResponse(episodes.Select(x =>
            Episode.FromSpotify(
                x.Id,
                x.Name.Trim(),
                htmlSanitiser.Sanitise(x.HtmlDescription),
                TimeSpan.FromMilliseconds(x.DurationMs),
                x.Explicit,
                x.GetReleaseDate(),
                new Uri(x.ExternalUrls.FirstOrDefault().Value, UriKind.Absolute),
                x.GetBestImageUrl()
            )
        ).ToList(), expensiveQueryFound);
    }
}
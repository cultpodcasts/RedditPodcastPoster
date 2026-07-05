using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Episodes.Adapters;
using RedditPodcastPoster.Episodes.Adapters.Inputs;
using RedditPodcastPoster.Episodes.Factories;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Spotify.Extensions;
using RedditPodcastPoster.PodcastServices.Spotify.Mapping;
using RedditPodcastPoster.PodcastServices.Spotify.Models;
using RedditPodcastPoster.Text;

namespace RedditPodcastPoster.PodcastServices.Spotify.Providers;

public class SpotifyEpisodeProvider(
    ISpotifyPodcastEpisodesProvider spotifyPodcastEpisodesProvider,
    IHtmlSanitiser htmlSanitiser,
    IEpisodeCatalogueAdapter<SpotifyCatalogueInput> spotifyEpisodeAdapter,
    IEpisodeFromCandidateFactory episodeFromCandidateFactory,
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

        return new GetEpisodesResponse(
            episodes.Select(MapEpisode).ToList(),
            expensiveQueryFound);
    }

    private Episode MapEpisode(SpotifyAPI.Web.SimpleEpisode episode)
    {
        var candidate = spotifyEpisodeAdapter.Adapt(episode.ToCatalogueInput(htmlSanitiser));
        return episodeFromCandidateFactory.Create(candidate, episode.Explicit);
    }
}

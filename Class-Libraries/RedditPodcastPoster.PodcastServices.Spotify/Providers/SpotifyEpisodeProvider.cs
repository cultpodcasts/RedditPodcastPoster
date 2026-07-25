using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Episodes.Adapters;
using RedditPodcastPoster.Episodes.Adapters.Inputs;
using RedditPodcastPoster.Episodes.Factories;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Spotify.Extensions;
using RedditPodcastPoster.PodcastServices.Spotify.Logging;
using RedditPodcastPoster.PodcastServices.Spotify.Mapping;
using RedditPodcastPoster.PodcastServices.Spotify.Models;
using RedditPodcastPoster.Text;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.Text.Sanitisers;

namespace RedditPodcastPoster.PodcastServices.Spotify.Providers;

public class SpotifyEpisodeProvider(
    ISpotifyPodcastEpisodesProvider spotifyPodcastEpisodesProvider,
    IHtmlSanitiser htmlSanitiser,
    IEpisodeCatalogueAdapter<SpotifyCatalogueInput> spotifyEpisodeAdapter,
    IEpisodeFromCandidateFactory episodeFromCandidateFactory,
    ILogger<SpotifyEpisodeProvider> logger)
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

        var market = request.Market ?? Market.CountryCode;
        episodes = episodes.Where(x => IsFreeSpotifyEpisode(x, market)).ToList();

        return new GetEpisodesResponse(
            episodes.Select(MapEpisode).ToList(),
            expensiveQueryFound);
    }

    private bool IsFreeSpotifyEpisode(SpotifyAPI.Web.SimpleEpisode episode, string market)
    {
        if (episode.IsSpotifyFree())
        {
            return true;
        }

        SpotifyNonPlayableSkipLogger.Log(logger, episode, market);
        return false;
    }

    private Episode MapEpisode(SpotifyAPI.Web.SimpleEpisode episode)
    {
        var candidate = spotifyEpisodeAdapter.Adapt(episode.ToCatalogueInput(htmlSanitiser));
        return episodeFromCandidateFactory.Create(candidate, episode.Explicit);
    }
}

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
using RedditPodcastPoster.PodcastServices.Abstractions.Models;

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

        episodes = episodes.Where(IsFreeSpotifyEpisode).ToList();

        return new GetEpisodesResponse(
            episodes.Select(MapEpisode).ToList(),
            expensiveQueryFound);
    }

    private bool IsFreeSpotifyEpisode(SpotifyAPI.Web.SimpleEpisode episode)
    {
        if (episode.IsSpotifyFree())
        {
            return true;
        }

        logger.LogWarning(
            "Skipping Spotify episode '{EpisodeId}' ('{EpisodeName}') because it is not free/playable (IsPlayable=false, restrictions.reason={RestrictionReason}).",
            episode.Id,
            episode.Name,
            episode.GetSpotifyRestrictionReason());
        return false;
    }

    private Episode MapEpisode(SpotifyAPI.Web.SimpleEpisode episode)
    {
        var candidate = spotifyEpisodeAdapter.Adapt(episode.ToCatalogueInput(htmlSanitiser));
        return episodeFromCandidateFactory.Create(candidate, episode.Explicit);
    }
}

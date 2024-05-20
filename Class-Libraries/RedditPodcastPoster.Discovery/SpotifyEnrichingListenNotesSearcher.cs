using Microsoft.Extensions.Logging;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.ListenNotes;
using RedditPodcastPoster.PodcastServices.Spotify;

namespace RedditPodcastPoster.Discovery;

public class SpotifyEnrichingListenNotesSearcher(
    IListenNotesSearcher listenNotesSearcher,
    ISpotifyEpisodeResolver spotifyEpisodeResolver,
    ILogger<SpotifyEnrichingListenNotesSearcher> logger
) : ISpotifyEnrichingListenNotesSearcher
{
    public async Task<IEnumerable<EpisodeResult>> Search(
        string query,
        IndexingContext indexingContext,
        bool enrichFromSpotify)
    {
        logger.LogInformation($"{nameof(Search)}: searching '{query}'. Enrich-from-spotify: '{enrichFromSpotify}'.");
        var episodeResults = await listenNotesSearcher.Search(query, indexingContext);
        if (enrichFromSpotify)
        {
            var results = new List<EpisodeResult>();
            foreach (var episodeResult in episodeResults)
            {
                var episodeRequest = new FindSpotifyEpisodeRequest(
                    string.Empty,
                    episodeResult.ShowName,
                    string.Empty,
                    episodeResult.EpisodeName,
                    episodeResult.Released,
                    true);

                var spotifyResult = await spotifyEpisodeResolver.FindEpisode(
                    episodeRequest, indexingContext);
                if (spotifyResult.FullEpisode != null)
                {
                    var enrichedResult = episodeResult with
                    {
                        Url = spotifyResult.FullEpisode.GetUrl(),
                        DiscoverService = DiscoverService.Spotify,
                        ServicePodcastId = spotifyResult.FullEpisode.Show.Id
                    };
                    results.Add(enrichedResult);
                }
                else
                {
                    results.Add(episodeResult);
                }
            }

            logger.LogInformation($"{nameof(Search)}: Found {results.Count} items from listen-notes enriched-from-spotify matching query '{query}'.");
            return results;
        }

        logger.LogInformation($"{nameof(Search)}: Found {episodeResults.Count()} items from listen-notes not-enriched-from-spotify matching query '{query}'.");
        return episodeResults;
    }
}
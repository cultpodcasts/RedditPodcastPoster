using Microsoft.Extensions.Logging;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.ListenNotes;
using RedditPodcastPoster.PodcastServices.Spotify;

namespace RedditPodcastPoster.Discovery;

public class SpotifyEnrichingListenNotesSearcher(
    IListenNotesSearcher listenNotesSearcher,
    ISpotifyEpisodeResolver spotifyEpisodeResolver,
#pragma warning disable CS9113 // Parameter is unread.
    ILogger<SpotifyEnrichingListenNotesSearcher> logger
#pragma warning restore CS9113 // Parameter is unread.
) : ISpotifyEnrichingListenNotesSearcher
{
    public async Task<IEnumerable<EpisodeResult>> Search(
        string query,
        IndexingContext indexingContext,
        bool enrichFromSpotify)
    {
        var results = new List<EpisodeResult>();
        var episodeResults = await listenNotesSearcher.Search(query, indexingContext);
        if (enrichFromSpotify)
        {
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

            return results;
        }

        return episodeResults;
    }
}
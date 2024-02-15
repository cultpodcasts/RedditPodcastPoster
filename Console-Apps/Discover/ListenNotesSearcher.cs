using Microsoft.Extensions.Logging;
using PodcastAPI;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.ListenNotes.Factories;
using RedditPodcastPoster.PodcastServices.ListenNotes.Model;
using RedditPodcastPoster.PodcastServices.Spotify;
using RedditPodcastPoster.Text;

namespace Discover;

public class ListenNotesSearcher(
    IClientFactory clientFactory,
    ISpotifyEpisodeResolver spotifyEpisodeResolver,
    IHtmlSanitiser htmlSanitiser,
    ILogger<ListenNotesSearcher> logger
) : IListenNotesSearcher
{
    private readonly Client _client = clientFactory.Create();

    public async Task<IEnumerable<EpisodeResult>> Search(string term, IndexingContext indexingContext)
    {
        var results = new List<EpisodeResult>();
        var offset = 0;
        var error = false;
        var parameters = new Dictionary<string, string>
        {
            {"q", term},
            {"type", "episode"},
            {"sort_by_date", "1"}
        };
        var first = true;
        while ((first || results.Last().Released > indexingContext.ReleasedSince) && !error)
        {
            first = false;
            parameters["offset"] = offset.ToString();
            try
            {
                var apiResponse = await _client.Search(parameters);
                var response = apiResponse.ToJSON<ListenNotesResponse>();
                var episodeResults = response.Results.Select(ToEpisodeResult);
                foreach (var episodeResult in episodeResults)
                {
                    var episodeRequest = new FindSpotifyEpisodeRequest(
                        string.Empty,
                        episodeResult.ShowName,
                        string.Empty,
                        episodeResult.EpisodeName,
                        episodeResult.Released,
                        true);
                    //var spotifyResult = await spotifyEpisodeResolver.FindEpisode(
                    //    episodeRequest, indexingContext);
                    //if (spotifyResult.FullEpisode != null)
                    //{
                    //    var enrichedResult = episodeResult with {Url = spotifyResult.FullEpisode.GetUrl()};
                    //    results.Add(enrichedResult);
                    //}
                    //else
                    //{
                    results.Add(episodeResult);
                    //}
                }

                offset = response.NextOffset;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, $"Error calling ListenNotes-api with parameters: {parameters}.");
                error = true;
            }
        }

        return results;
    }

    private EpisodeResult ToEpisodeResult(ListenNotesEpisode episode)
    {
        return new EpisodeResult(
            episode.Id,
            DateTimeOffset.FromUnixTimeMilliseconds(episode.ReleasedMilliseconds).DateTime,
            htmlSanitiser.Sanitise(episode.Description).Trim(),
            episode.Title.Trim(),
            episode.Podcast.ShowName.Trim());
    }
}
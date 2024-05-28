using Microsoft.Extensions.Logging;
using PodcastAPI;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.ListenNotes.Factories;
using RedditPodcastPoster.PodcastServices.ListenNotes.Model;
using RedditPodcastPoster.Text;

namespace RedditPodcastPoster.PodcastServices.ListenNotes;

public class ListenNotesSearcher(
    IClientFactory clientFactory,
    IHtmlSanitiser htmlSanitiser,
    ILogger<ListenNotesSearcher> logger
) : IListenNotesSearcher
{
    private const string QueryKey = "q";
    private const string OffsetKey = "offset";
    private readonly Client _client = clientFactory.Create();

    public async Task<IList<EpisodeResult>> Search(string term, IndexingContext indexingContext)
    {
        var results = new List<EpisodeResult>();
        var offset = 0;
        var error = false;
        var @break = false;
        var parameters = new Dictionary<string, string>
        {
            {QueryKey, $@"""{term}"""},
            {"type", "episode"},
            {"sort_by_date", "1"}
        };
        var first = true;
        while (!error && !@break && (first || results.Last().Released > indexingContext.ReleasedSince))
        {
            first = false;
            parameters[OffsetKey] = offset.ToString();
            try
            {
                var apiResponse = await _client.Search(parameters);
                var response = apiResponse.ToJSON<ListenNotesResponse>();
                var episodeResults = response.Results.Select(ToEpisodeResult);
                results.AddRange(episodeResults);

                offset = response.NextOffset;
                @break = offset == 0;
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    $"Error calling ListenNotes-api with parameters: query:'{parameters[QueryKey]}', offset:'{parameters[OffsetKey]}'.");
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
            TimeSpan.FromSeconds(episode.AudioLengthSeconds),
            episode.Podcast.ShowName.Trim(),
            DiscoverService.ListenNotes);
    }
}
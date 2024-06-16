using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PodcastAPI;
using RedditPodcastPoster.Configuration.Extensions;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.ListenNotes.Configuration;
using RedditPodcastPoster.PodcastServices.ListenNotes.Factories;
using RedditPodcastPoster.PodcastServices.ListenNotes.Model;
using RedditPodcastPoster.Text;

namespace RedditPodcastPoster.PodcastServices.ListenNotes;

public class ListenNotesSearcher(
    IClientFactory clientFactory,
    IHtmlSanitiser htmlSanitiser,
    IOptions<ListenNotesOptions> listenNotesOptions,
    ILogger<ListenNotesSearcher> logger
) : IListenNotesSearcher
{
    private const string QueryKey = "q";
    private const string OffsetKey = "offset";
    private readonly Client _client = clientFactory.Create();

    private readonly ListenNotesOptions _listenNotesOptions = listenNotesOptions.Value;

    public async Task<IList<EpisodeResult>> Search(string term, IndexingContext indexingContext)
    {
        logger.LogInformation($"{nameof(ListenNotesSearcher)}.{nameof(Search)}: query: '{term}'. RequestDelaySeconds='{listenNotesOptions.Value.RequestDelaySeconds}'.");
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

        if (_listenNotesOptions.UsePublishedAfter)
        {
            var releasedSince = indexingContext.ReleasedSince!.Value.ToEpochMilliseconds();
            parameters.Add("published_after", releasedSince.ToString());
        }

        var listenNotesRequestDelay = TimeSpan.FromSeconds(Convert.ToDouble(_listenNotesOptions.RequestDelaySeconds));

        var first = true;
        while (!error && !@break && (first || results.Last().Released > indexingContext.ReleasedSince))
        {
            logger.LogInformation($"Querying listen-notes for '{term}'. First={first}.");
            if (!first)
            {
                await Task.Delay(listenNotesRequestDelay);
            }

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
                results = results.OrderByDescending(x => x.Released).ToList();
                logger.LogInformation($"Last gathered listen-notes released-date: '{results.Last().Released:G}'.");
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
        var episodeResult = new EpisodeResult(
            episode.Id,
            DateTimeOffset.FromUnixTimeMilliseconds(episode.ReleasedMilliseconds).DateTime,
            htmlSanitiser.Sanitise(episode.Description).Trim(),
            episode.Title.Trim(),
            TimeSpan.FromSeconds(episode.AudioLengthSeconds),
            episode.Podcast.ShowName.Trim(),
            string.Empty,
            DiscoverService.ListenNotes,
            itunesPodcastId: episode.PodcastITunesId);
        episodeResult.PodcastIds.Apple ??= episode.PodcastITunesId;
        return episodeResult;
    }
}
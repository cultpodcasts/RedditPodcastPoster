using GraphQL;
using GraphQL.Client.Http;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Configuration.Extensions;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Taddy.Dtos;
using RedditPodcastPoster.Text;

namespace RedditPodcastPoster.PodcastServices.Taddy;

public class TaddySearcher(
    GraphQLHttpClient graphQlClient,
    IHtmlSanitiser htmlSanitiser,
    ILogger<TaddySearcher> logger
) : ITaddySearcher
{
    public async Task<IList<EpisodeResult>> Search(string term, IndexingContext indexingContext)
    {
        logger.LogInformation($"{nameof(TaddySearcher)}.{nameof(Search)}: query: '{term}'.");

        var since = indexingContext.ReleasedSince!.Value.ToEpochSeconds();

        var query = $@"
                   {{
                     searchForTerm(
                       term: ""{term}""
                       filterForTypes: PODCASTEPISODE
                       sortByDatePublished: LATEST
                       filterForPublishedAfter: {since}
                     ) {{
                       searchId
                       podcastEpisodes {{
                         uuid
                         name
                         description
                         duration
                         datePublished
                         audioUrl
                         podcastSeries {{
                           uuid
                           name
                         }}
                       }}
                     }}
                   }}
                   ";

        var episodesRequest = new GraphQLRequest
        {
            Query = query
        };

        var response = await graphQlClient.SendQueryAsync<SearchResponse>(episodesRequest);
        if (response.Errors != null && response.Errors.Any())
        {
            logger.LogError(
                $"Error querying taddy: {string.Join(", ", response.Errors.Select(x => $"Message: '{x.Message}''"))}");
        }

        return response.Data.Results.Episodes.Select(ToEpisodeResult).ToList();
    }


    private EpisodeResult ToEpisodeResult(PodcastEpisode episode)
    {
        var episodeResult = new EpisodeResult(
            episode.Id.ToString(),
            DateTimeOffset.FromUnixTimeSeconds(episode.Published).DateTime,
            htmlSanitiser.Sanitise(episode.Description).Trim(),
            episode.Name.Trim(),
            TimeSpan.FromSeconds(episode.Seconds),
            episode.Podcast.Name.Trim(),
            string.Empty,
            DiscoverService.Taddy);
        return episodeResult;
    }
}
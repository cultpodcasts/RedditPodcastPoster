using GraphQL;
using GraphQL.Client.Http;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Configuration.Extensions;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Taddy.Dtos;
using RedditPodcastPoster.Text;
using PodcastEpisode = RedditPodcastPoster.PodcastServices.Taddy.Dtos.PodcastEpisode;

namespace RedditPodcastPoster.PodcastServices.Taddy;

public class TaddySearcher(
    GraphQLHttpClient graphQlClient,
    IHtmlSanitiser htmlSanitiser,
    ILogger<TaddySearcher> logger
) : ITaddySearcher
{
    public async Task<IList<EpisodeResult>> Search(string term, IndexingContext indexingContext)
    {
        logger.LogInformation("{nameofTaddySearcher}.{nameofSearch}: query: '{term}'.",
            nameof(TaddySearcher), nameof(Search), term);

        var since = indexingContext.ReleasedSince!.Value.Subtract(TaddyParameters.IndexingDelay).ToEpochSeconds();

        // ReSharper disable once StringLiteralTypo
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
        try
        {
            var response = await graphQlClient.SendQueryAsync<SearchResponse>(episodesRequest);
            if (response.Errors != null && response.Errors.Any())
            {
                logger.LogError(
                    "Error querying taddy: {errors}",
                    string.Join(", ", response.Errors.Select(x => $"Message: '{x.Message}''")));
            }

            if (response.Data?.Results?.Episodes == null)
            {
                throw new InvalidOperationException("Null data-response from taddy.");
            }

            return response.Data.Results.Episodes.Select(ToEpisodeResult).ToList();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{nameofSearch}: Failed to query taddy for '{term}'.",
                nameof(Search), term);
            return [];
        }
    }

    private EpisodeResult ToEpisodeResult(PodcastEpisode episode)
    {
        var episodeResult = new EpisodeResult(
            episode.Id.ToString(),
            DateTimeOffset.FromUnixTimeSeconds(episode.Published).DateTime,
            htmlSanitiser.Sanitise(episode.Description ?? string.Empty).Trim(),
            episode.Name?.Trim() ?? string.Empty,
            episode.Seconds.HasValue ? TimeSpan.FromSeconds(episode.Seconds.Value) : null,
            episode.Podcast?.Name.Trim() ?? string.Empty,
            string.Empty,
            DiscoverService.Taddy);
        return episodeResult;
    }
}
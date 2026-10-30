using FluentAssertions;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.News;
using RedditPodcastPoster.Models.TvShows;
using RedditPodcastPoster.Persistence.Abstractions.Episodes;
using RedditPodcastPoster.Persistence.Providers;

namespace RedditPodcastPoster.Persistence.Tests;

/// <summary>
/// #1102: the shared recently-active publisher filter (used for podcasts by HomepagePublisher and
/// RecentEpisodeCandidatesProvider, and for TV shows / news organisations) must select a stored
/// <c>"removed": null</c>, because every C# reader treats null as not removed.
/// </summary>
public class PublisherCosmosFiltersRemovedNullRules : IDisposable
{
    private readonly CosmosClient _client = new(
        "https://synthetic.documents.example:443/",
        Convert.ToBase64String(Guid.NewGuid().ToByteArray()),
        new CosmosClientOptions
        {
            UseSystemTextJsonSerializerWithOptions = new JsonSerializerOptionsProvider().GetJsonSerializerOptions()
        });

    public void Dispose() => _client.Dispose();

    private string ToCosmosSql<TPublisher>() where TPublisher : Publisher =>
        _client.GetContainer("db", "publishers").GetItemLinqQueryable<TPublisher>()
            .Where(PublisherCosmosFilters.ReleasedOnOrAfter<TPublisher>(DomainTestFixture.UtcDaysAgo(7)))
            .ToQueryDefinition()
            .QueryText;

    [Fact(DisplayName =
        "Recently-active podcasts: the Cosmos query selects a stored \"removed\": null as well as absent or false, " +
        "because C# treats null as not removed and Cosmos drops null under 'removed = false' alone (#1102).")]
    public void recent_podcast_query_selects_a_stored_removed_null()
    {
        // Arrange
        // The filter is the shared recently-active publisher predicate; the client is synthetic.

        // Act
        var sql = ToCosmosSql<Podcast>();

        // Assert
        sql.Should().Contain("NOT IS_DEFINED(root[\"removed\"])");
        sql.Should().Contain("(root[\"removed\"] = null)");
        sql.Should().Contain("(root[\"removed\"] = false)");
    }

    [Fact(DisplayName =
        "Recently-active TV shows and news organisations: the same filter keeps the null disjunct, because all " +
        "publishers share one 'not removed' rule (#1102).")]
    public void recent_parent_queries_select_a_stored_removed_null()
    {
        // Arrange
        // TV shows and news organisations use the same publisher filter.

        // Act
        var sqls = new[] { ToCosmosSql<TvShow>(), ToCosmosSql<NewsOrganisation>() };

        // Assert
        sqls.Should().OnlyContain(sql => sql.Contains("(root[\"removed\"] = null)"));
    }
}

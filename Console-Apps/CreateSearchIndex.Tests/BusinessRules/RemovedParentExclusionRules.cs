using CreateSearchIndex;
using FluentAssertions;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Persistence.Configuration;

namespace CreateSearchIndex.Tests;

/// <summary>
/// Guardrail: the search pull SQL must never index a playable whose parent (podcast, TV show or news
/// organisation) is removed. Synthetic container names only.
/// </summary>
public class RemovedParentExclusionRules
{
    private static string WhereClause(string query)
    {
        var where = query.IndexOf(" WHERE ", StringComparison.Ordinal);
        where.Should().BePositive("the pull SQL needs a WHERE clause");
        return query[where..];
    }

    [Fact(DisplayName =
        "The episode data source SQL excludes episodes whose podcast is removed, using parentRemoved (absent, null or false = kept), " +
        "because episodes of removed podcasts must never be indexed in search.")]
    public void episode_datasource_sql_excludes_parentRemoved_episodes()
    {
        // Arrange
        var query = CreateSearchIndexProcessor.EpisodeDataSourceQuery();

        // Act
        var where = WhereClause(query);

        // Assert
        where.Should().Contain(Playable.CosmosParentNotRemovedSql);
        where.Should().Contain("(NOT IS_DEFINED(e.parentRemoved) OR IS_NULL(e.parentRemoved) OR e.parentRemoved=false)");
    }

    [Fact(DisplayName =
        "The episode data source SQL also excludes episodes carrying the retired podcastRemoved key, " +
        "because the EpisodeWireRewrite rollback writes podcastRemoved: true back and a removed podcast's episodes must stay out of search in either wire state.")]
    public void episode_datasource_sql_keeps_legacy_podcastRemoved_guard()
    {
        // Arrange
        var query = CreateSearchIndexProcessor.EpisodeDataSourceQuery();

        // Act
        var where = WhereClause(query);

        // Assert
        where.Should().Contain("(NOT IS_DEFINED(e.podcastRemoved) OR IS_NULL(e.podcastRemoved) OR e.podcastRemoved=false)");
    }

    [Fact(DisplayName =
        "The episode data source SQL ANDs the removed-podcast exclusions with the removed-episode exclusion before the high-water mark, " +
        "so no OR branch can let a removed podcast's episode through.")]
    public void episode_datasource_where_clause_is_and_of_exclusions()
    {
        // Arrange
        var query = CreateSearchIndexProcessor.EpisodeDataSourceQuery();

        // Act
        var where = WhereClause(query);

        // Assert
        where.Should().Be(
            " WHERE ((NOT IS_DEFINED(e.parentRemoved) OR IS_NULL(e.parentRemoved) OR e.parentRemoved=false)" +
            " AND (NOT IS_DEFINED(e.podcastRemoved) OR IS_NULL(e.podcastRemoved) OR e.podcastRemoved=false))" +
            " and ((NOT IS_DEFINED(e.removed)) OR e.removed=false) and e._ts >= @HighWaterMark ORDER BY e._ts");
    }

    [Fact(DisplayName =
        "The TV show episode and news report data source SQL excludes items whose parent is removed (parentRemoved) and removed items, " +
        "because a removed TV show's or news organisation's playables must never be indexed in search.")]
    public void sibling_datasource_sql_excludes_removed_parents()
    {
        // Arrange
        var settings = new CosmosDbSettings
        {
            Endpoint = "https://synthetic.documents.example/", AuthKeyOrResourceToken = "synthetic",
            DatabaseId = "db", PodcastsContainer = "podcasts", EpisodesContainer = "episodes",
            TvShowsContainer = "tvshows", TvShowEpisodesContainer = "tvshowepisodes", FilmsContainer = "films",
            NewsOrganisationsContainer = "newsorganisations", NewsReportsContainer = "newsreports",
            SubjectsContainer = "subjects", PeopleContainer = "people", ActivitiesContainer = "activities",
            DiscoveryContainer = "discovery", DiscoverySubmissionsContainer = "submissions",
            LookUpsContainer = "lookups", TitleCasingRulesContainer = "titlecasing",
            PushSubscriptionsContainer = "push"
        };

        // Act
        var siblings = PlayableSearchSources.Siblings(settings, "synthetic-index");

        // Assert
        siblings.Should().HaveCount(2);
        foreach (var sibling in siblings)
        {
            var where = WhereClause(sibling.Query);
            where.Should().StartWith(
                " WHERE ((NOT IS_DEFINED(c.parentRemoved) OR IS_NULL(c.parentRemoved) OR c.parentRemoved = false)" +
                " AND (NOT IS_DEFINED(c.removed) OR c.removed = false))",
                $"{sibling.ContentKind} must exclude removed parents and removed items");
        }
    }
}

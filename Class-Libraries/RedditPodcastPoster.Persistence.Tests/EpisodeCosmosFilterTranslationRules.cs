using FluentAssertions;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.TvShows;
using RedditPodcastPoster.Persistence.Abstractions.Episodes;
using RedditPodcastPoster.Persistence.Providers;

namespace RedditPodcastPoster.Persistence.Tests;

public class EpisodeCosmosFilterTranslationRules
{
    private readonly DomainTestFixture _fixture = new();

    private CosmosClient CreateClient() =>
        new(
            "https://synthetic.documents.example:443/",
            Convert.ToBase64String(_fixture.CreateGuid().ToByteArray()),
            new CosmosClientOptions
            {
                UseSystemTextJsonSerializerWithOptions = new JsonSerializerOptionsProvider().GetJsonSerializerOptions()
            });

    [Fact(DisplayName =
        "When episodes are filtered by ReleasedOnOrAfter, then the Cosmos SQL compares releaseSort only and never reads the legacy release fallback, because the GATE 5 bridge drop relies on every stored episode carrying releaseSort.")]
    public void released_on_or_after_translates_to_release_sort_only()
    {
        // Arrange
        var since = DomainTestFixture.UtcDaysAgo(_fixture.Create<int>() % 30 + 1);
        using var client = CreateClient();
        var container = client.GetContainer("db", "episodes");

        // Act
        var sql = container.GetItemLinqQueryable<Episode>()
            .Where(EpisodeCosmosFilters.ReleasedOnOrAfter(since))
            .ToQueryDefinition()
            .QueryText;

        // Assert
        sql.Should().Contain("[\"releaseSort\"] >=");
        sql.Should().NotContain("[\"release\"]");
        sql.Should().NotContain("IS_DEFINED");
    }

    [Fact(DisplayName =
        "When episodes are filtered by ParentNotRemoved, then the Cosmos SQL reads parentRemoved and never the retired podcastRemoved key, because the GATE 1 wire rewrite moved that flag to parentRemoved.")]
    public void parent_not_removed_translates_to_parent_removed_only()
    {
        // Arrange
        using var client = CreateClient();
        var container = client.GetContainer("db", "episodes");

        // Act
        var sql = container.GetItemLinqQueryable<Episode>()
            .Where(EpisodeCosmosFilters.ParentNotRemoved)
            .ToQueryDefinition()
            .QueryText;

        // Assert
        sql.Should().Contain("[\"parentRemoved\"]");
        sql.Should().NotContain("podcastRemoved");
    }

    [Fact(DisplayName =
        "When TV show episodes are filtered by PlayableCosmosFilters.ReleasedOnOrAfter, then the Cosmos SQL compares releaseSort only, because new playable containers were always written with releaseSort.")]
    public void playable_released_on_or_after_translates_to_release_sort_only()
    {
        // Arrange
        var since = DomainTestFixture.UtcDaysAgo(_fixture.Create<int>() % 30 + 1);
        using var client = CreateClient();
        var container = client.GetContainer("db", "tvShowEpisodes");

        // Act
        var sql = container.GetItemLinqQueryable<TvShowEpisode>()
            .Where(PlayableCosmosFilters.ReleasedOnOrAfter<TvShowEpisode>(since))
            .ToQueryDefinition()
            .QueryText;

        // Assert
        sql.Should().Contain("[\"releaseSort\"] >=");
        sql.Should().NotContain("[\"release\"]");
    }
}

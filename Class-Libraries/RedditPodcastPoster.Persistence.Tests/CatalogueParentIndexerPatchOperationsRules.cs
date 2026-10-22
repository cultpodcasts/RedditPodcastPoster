using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using Microsoft.Azure.Cosmos;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.Persistence.Providers;
using RedditPodcastPoster.Persistence.Repositories;

namespace RedditPodcastPoster.Persistence.Tests;

public class CatalogueParentIndexerPatchOperationsRules
{
    private readonly DomainTestFixture _fixture = new();

    private CatalogueIndexingSource CreateIndexing() =>
        CatalogueIndexingSource.FromPodcast(_fixture.CreatePodcast());

    [Fact(DisplayName =
        "Indexer patch: operations only set /lastIndexed, /latestReleased and the changed /indexing members, " +
        "because curator-owned fields (name, description, publisher, removed) must never be written by the Indexer.")]
    public void operations_touch_only_indexer_owned_paths()
    {
        // Arrange
        var indexing = CreateIndexing();
        var patch = new CatalogueParentIndexerPatch(
            DomainTestFixture.UtcDaysAgo(0),
            DomainTestFixture.UtcDaysAgo(1),
            indexing,
            ["youTubePlaylistQueryIsExpensive", "spotifyEpisodeCount"]);

        // Act
        var operations = CatalogueParentIndexerPatchOperations.Build(patch, new JsonSerializerOptions())
            .SelectMany(x => x).ToList();

        // Assert
        operations.Should().OnlyContain(x => x.OperationType == PatchOperationType.Set);
        operations.Select(x => x.Path).Should().BeEquivalentTo(
            "/lastIndexed", "/latestReleased", "/indexing/spotifyEpisodeCount", "/indexing/youTubePlaylistQueryIsExpensive");
    }

    [Fact(DisplayName =
        "Indexer patch: an unchanged field is not written, so a pass that only completed patches /lastIndexed " +
        "alone, because every extra path is a chance to overwrite a concurrent curator edit.")]
    public void unchanged_fields_are_not_written()
    {
        // Arrange
        var patch = new CatalogueParentIndexerPatch(DomainTestFixture.UtcDaysAgo(0), null, CreateIndexing(), []);

        // Act
        var operations = CatalogueParentIndexerPatchOperations.Build(patch, new JsonSerializerOptions())
            .SelectMany(x => x).ToList();

        // Assert
        operations.Select(x => x.Path).Should().Equal("/lastIndexed");
        patch.IsEmpty.Should().BeFalse();
        new CatalogueParentIndexerPatch(null, null, patch.Indexing, []).IsEmpty.Should().BeTrue();
    }

    [Fact(DisplayName =
        "Indexer patch: an indexing member is written with the same wire shape a full save would give it (enum as " +
        "string), because the patched document must stay readable by every reader.")]
    public void indexing_values_keep_their_wire_shape()
    {
        // Arrange
        var indexing = CreateIndexing();
        indexing.ReleaseAuthority = Service.YouTube;
        var patch = new CatalogueParentIndexerPatch(null, null, indexing, ["releaseAuthority"]);

        // Act
        var operation = CatalogueParentIndexerPatchOperations.Build(patch, new JsonSerializerOptions())
            .SelectMany(x => x).Single();

        // Assert
        operation.Path.Should().Be("/indexing/releaseAuthority");
        var value = ((PatchOperation<JsonNode?>) operation).Value;
        value!.GetValue<string>().Should().Be(nameof(Service.YouTube));
    }

    [Fact(DisplayName =
        "Indexer patch: more changes than Cosmos allows in one patch are split into chunks of at most ten, because " +
        "Cosmos rejects a patch with more than ten operations.")]
    public void operations_are_chunked_to_the_cosmos_limit()
    {
        // Arrange
        var names = JsonSerializer.SerializeToNode(CreateIndexing())!.AsObject().Select(x => x.Key).ToList();
        var patch = new CatalogueParentIndexerPatch(DomainTestFixture.UtcDaysAgo(0), DomainTestFixture.UtcDaysAgo(1), CreateIndexing(), names);

        // Act
        var chunks = CatalogueParentIndexerPatchOperations.Build(patch, new JsonSerializerOptions());

        // Assert
        names.Count.Should().BeGreaterThan(CatalogueParentIndexerPatchOperations.MaxOperationsPerPatch);
        chunks.Should().OnlyContain(x => x.Count <= CatalogueParentIndexerPatchOperations.MaxOperationsPerPatch);
        chunks.SelectMany(x => x).Should().HaveCount(names.Count + 2);
    }

    [Fact(DisplayName =
        "latestReleased raise: the patch filter only matches a missing, null or older stored value, written in the " +
        "container's own wire shape, because a value another writer already raised must never be lowered again.")]
    public void latest_released_filter_only_matches_missing_or_older_values()
    {
        // Arrange
        var options = new JsonSerializerOptionsProvider().GetJsonSerializerOptions();
        var released = DomainTestFixture.UtcDaysAgo(1);
        var wire = JsonSerializer.SerializeToElement(released, options).GetString();

        // Act
        var filter = CatalogueParentIndexerPatchOperations.LatestReleasedOlderThanFilter(released, options);

        // Assert
        filter.Should().StartWith("FROM c WHERE ");
        filter.Should().Contain("NOT IS_DEFINED(c.latestReleased)");
        filter.Should().Contain("IS_NULL(c.latestReleased)");
        filter.Should().EndWith($"c.latestReleased < '{wire}'");
        wire.Should().MatchRegex(@"^\d{4}-\d{2}-\d{2}T");
    }
}

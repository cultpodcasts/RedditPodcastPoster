using FluentAssertions;
using RedditPodcastPoster.Models.Podcasts;
using Xunit;

namespace Indexer.Tests.BusinessRules;

public class SearchIndexCosmosSqlRules
{
    [Fact(DisplayName =
        "Empty search-encoded keys must not produce pull-path SQL, because an unloaded catalog would emit a blank svc projection and silently drop streaming URLs from Azure Search.")]
    public void empty_search_encoded_keys_must_not_produce_pull_path_sql()
    {
        // Arrange
        IReadOnlyList<string> empty = [];

        // Act
        var svc = () => SearchIndexCosmosSql.SvcProjection(empty);
        var image = () => SearchIndexCosmosSql.CoalescedImageFallback(empty);

        // Assert
        svc.Should().Throw<ArgumentException>().WithParameterName("searchEncodedKeys");
        image.Should().Throw<ArgumentException>().WithParameterName("imageCoalesceOrder");
    }

    [Fact(DisplayName =
        "A non-identifier catalog key must not be interpolated into pull-path SQL, because Cosmos field paths are not quoted.")]
    public void non_identifier_key_must_not_produce_pull_path_sql()
    {
        // Arrange
        IReadOnlyList<string> bad = ["bad-key"];

        // Act
        var act = () => SearchIndexCosmosSql.SvcProjection(bad);

        // Assert
        act.Should().Throw<ArgumentException>().WithParameterName("searchEncodedKeys");
    }
}

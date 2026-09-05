using FluentAssertions;
using RedditPodcastPoster.EntitySearchIndexer.Models;
using RedditPodcastPoster.Models.Podcasts;
using Xunit;

namespace Indexer.Tests;

public class SearchIndexCosmosSqlTests
{
    [Fact(DisplayName =
        "Cosmos pull-path svc SQL includes every ServiceCatalog.SearchEncodedKeys entry as e.services.{key}.url, " +
        "because a hardcoded list omitted streaming keys like itvx and left search svc empty after SubmitUrl.")]
    public void svc_projection_includes_every_search_encoded_key()
    {
        // Arrange
        // Act
        var sql = SearchIndexCosmosSql.SvcProjection();

        // Assert
        foreach (var key in ServiceCatalog.SearchEncodedKeys)
        {
            sql.Should().Contain(
                $@"e.services.{key}.url",
                because: $"SearchEncodedKeys entry '{key}' must appear in the Azure Search datasource svc projection");
            sql.Should().Contain(
                $@"""{key}:""",
                because: $"svc entries for '{key}' must use the catalog JSON key as the compact prefix");
        }

        sql.Should().StartWith("RTRIM(CONCAT(");
        sql.Should().Contain(ServiceKeys.Itvx);
        sql.Should().Contain(ServiceKeys.Channel4);
        sql.Should().Contain(ServiceKeys.DisneyPlus);
    }

    [Fact(DisplayName =
        "Cosmos pull-path image coalesce SQL walks ServiceCatalog.ImageCoalesceOrder so ITVX (and other " +
        "streaming) artwork is selected when Spotify/Apple/YouTube art is absent.")]
    public void image_fallback_includes_every_image_coalesce_order_key()
    {
        // Arrange
        // Act
        var sql = SearchIndexCosmosSql.CoalescedImageFallback();

        // Assert
        var expected = string.Join(
            " ?? ",
            ServiceCatalog.ImageCoalesceOrder.Select(key => $"e.services.{key}.image"));
        sql.Should().Be(expected);
        sql.Should().Contain($"e.services.{ServiceKeys.Itvx}.image");
        sql.Should().StartWith($"e.services.{ServiceKeys.YouTube}.image");
    }
}

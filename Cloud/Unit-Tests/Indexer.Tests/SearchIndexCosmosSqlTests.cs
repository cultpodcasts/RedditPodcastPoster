using FluentAssertions;
using RedditPodcastPoster.Models.Podcasts;
using Xunit;
using RedditPodcastPoster.PodcastServices.Abstractions.Streaming;

namespace Indexer.Tests;

public class SearchIndexCosmosSqlTests
{
    [Fact(DisplayName =
        "Cosmos pull-path svc SQL includes every StreamingServiceCatalog.SearchEncodedKeys entry as e.services.{key}.url, " +
        "because hardcoded lists could drift behind ServiceCatalog / live datasource SQL could lag the repo and leave search svc empty after SubmitUrl.")]
    public void svc_projection_includes_every_search_encoded_key()
    {
        // Arrange
        // Act
        var sql = SearchIndexCosmosSql.SvcProjection(StreamingServiceCatalog.SearchEncodedKeys);

        // Assert
        foreach (var key in StreamingServiceCatalog.SearchEncodedKeys)
        {
            sql.Should().Contain(
                $@"e.services.{key}.url",
                because: $"SearchEncodedKeys entry '{key}' must appear in the Azure Search datasource svc projection");
            sql.Should().Contain(
                $@"""{key}:""",
                because: $"svc entries for '{key}' must use the catalog JSON key as the compact prefix");
        }

        sql.Should().StartWith("RTRIM(CONCAT(");

        // Streaming matrix: every non-index-id ServiceKeys constant must be in SearchEncodedKeys
        // and therefore in svc SQL — not ITVX-only (discoveryPlus / disneyPlus / channel4 / …).
        var streamingKeys = new[]
        {
            StreamingServiceKeys.BbcSounds,
            StreamingServiceKeys.BbcIplayer,
            StreamingServiceKeys.InternetArchive,
            StreamingServiceKeys.Vimeo,
            StreamingServiceKeys.Netflix,
            StreamingServiceKeys.AmazonPrime,
            StreamingServiceKeys.ParamountPlus,
            StreamingServiceKeys.HboMax,
            StreamingServiceKeys.PlaySuisse,
            StreamingServiceKeys.PlayRts,
            StreamingServiceKeys.TvnzPlus,
            StreamingServiceKeys.Itvx,
            StreamingServiceKeys.Channel4,
            StreamingServiceKeys.Fawesome,
            StreamingServiceKeys.DisneyPlus,
            StreamingServiceKeys.BcVideo,
            StreamingServiceKeys.Tubi,
            StreamingServiceKeys.DiscoveryPlus
        };
        streamingKeys.Should().BeEquivalentTo(
            StreamingServiceCatalog.SearchEncodedKeys,
            because: "SearchEncodedKeys must list every streaming/catalog URL key that can appear under Episode.services");
        foreach (var key in streamingKeys)
        {
            sql.Should().Contain(
                $@"e.services.{key}.url",
                because: $"streaming key '{key}' must be in Cosmos datasource svc SQL so search is not empty after SubmitUrl");
        }

        StreamingServiceCatalog.SearchEncodedKeys.Should().NotContain(ServiceKeys.Spotify);
        StreamingServiceCatalog.SearchEncodedKeys.Should().NotContain(ServiceKeys.Apple);
        StreamingServiceCatalog.SearchEncodedKeys.Should().NotContain(ServiceKeys.YouTube);
    }

    [Fact(DisplayName =
        "Cosmos pull-path image coalesce SQL walks StreamingServiceCatalog.ImageCoalesceOrder for every catalog " +
        "service (including discoveryPlus and other streaming) when Spotify/Apple/YouTube art is absent.")]
    public void image_fallback_includes_every_image_coalesce_order_key()
    {
        // Arrange
        // Act
        var sql = SearchIndexCosmosSql.CoalescedImageFallback(StreamingServiceCatalog.ImageCoalesceOrder);

        // Assert
        var expected = string.Join(
            " ?? ",
            StreamingServiceCatalog.ImageCoalesceOrder.Select(key => $"e.services.{key}.image"));
        sql.Should().Be(expected);
        sql.Should().StartWith($"e.services.{ServiceKeys.YouTube}.image");
        foreach (var key in StreamingServiceCatalog.SearchEncodedKeys)
        {
            sql.Should().Contain(
                $"e.services.{key}.image",
                because: $"streaming key '{key}' must participate in image coalesce so search image is not empty");
        }
    }

    [Fact(DisplayName =
        "StreamingServiceCatalog.All keys minus index-id platforms equal SearchEncodedKeys so generated SQL cannot " +
        "silently omit a newly added streaming service.")]
    public void search_encoded_keys_cover_every_non_index_id_catalog_entry()
    {
        // Arrange
        var catalogNonIndexIdKeys = StreamingServiceCatalog.All
            .Select(d => d.Key)
            .Where(key => !ServiceCatalog.IsIndexIdKey(key))
            .ToArray();

        // Act
        var encoded = StreamingServiceCatalog.SearchEncodedKeys;

        // Assert
        encoded.Should().BeEquivalentTo(
            catalogNonIndexIdKeys,
            because: "every Episode.services catalog key except spotify/apple/youtube must be search-encoded");
    }
}

using CreateSearchIndex;
using FluentAssertions;
using RedditPodcastPoster.Models.Episodes;

namespace CreateSearchIndex.Tests;

/// <summary>
/// #1500: the episode pull SQL mirrors <c>SearchEpisodeImage.From</c> for a curator-removed image, so the scheduled
/// indexer never turns the push path's <c>-</c> back into <c>""</c> (which clients read as "use the YouTube thumbnail").
/// </summary>
public class ImageRemovedProjectionRules
{
    [Fact(DisplayName =
        "The episode data source SQL projects the '-' image token when e.imageRemoved is true, before any image coalescing, " +
        "because a curator-removed image must not fall back to the YouTube thumbnail (#1500).")]
    public void episode_datasource_sql_projects_removed_token_for_image_removed()
    {
        // Arrange
        var query = CreateSearchIndexProcessor.EpisodeDataSourceQuery();

        // Act
        var imageRemovedGuard =
            $"IIF(IS_DEFINED(e.imageRemoved) AND e.imageRemoved = true, \"{Episode.ImageRemovedSearchToken}\",";
        var guardIndex = query.IndexOf(imageRemovedGuard, StringComparison.Ordinal);
        var youTubeTokenIndex = query.IndexOf("CONCAT(\"y\"", StringComparison.Ordinal);

        // Assert
        guardIndex.Should().BePositive("the image projection starts with the removed-image guard");
        youTubeTokenIndex.Should().BeGreaterThan(guardIndex, "the removed guard wins over every image token");
        query.Should().Contain(" as image,");
    }
}

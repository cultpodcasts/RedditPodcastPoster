using FindDuplicateEpisodes;
using FluentAssertions;
using RedditPodcastPoster.Models.Catalogue;
using Xunit;

namespace FindDuplicateEpisodes.Tests.BusinessRules;

/// <summary>
/// Guardrail: FindDuplicateEpisodes Cosmos SQL must treat explicit <c>removed: null</c> as not removed (#1153).
/// </summary>
public class ActiveEpisodesFilterRules
{
    [Fact(DisplayName =
        "FindDuplicateEpisodes ActiveEpisodesFilter keeps an episode whose removed is absent, null or false, " +
        "because explicit null means the episode is not removed (same rule as Playable.IsRemoved / #1153) " +
        "and Cosmos null = false alone would exclude those rows from duplicate detection.")]
    public void active_episodes_filter_treats_null_removed_as_not_removed()
    {
        // Arrange
        var filter = FindDuplicateEpisodesProcessor.ActiveEpisodesFilter;

        // Act / Assert
        filter.Should().Contain(Playable.CosmosNotRemovedSql);
        filter.Should().Contain("IS_NULL(e.removed)");
        filter.Should().Contain(Playable.CosmosParentNotRemovedSql);
    }
}

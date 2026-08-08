using FluentAssertions;
using RedditPodcastPoster.EntitySearchIndexer;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using Xunit;

namespace Indexer.Tests.BusinessRules;

public class EpisodeSearchIndexEligibilityRules
{
    private readonly DomainTestFixture _fixture = new();

    [Fact(DisplayName =
        "Search eligibility: when the episode is Removed, exclude it from Azure Search so " +
        "elimination / admin removals delete the search document instead of re-uploading it.")]
    public void excludes_removed_episode()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var episode = _fixture.CreateEpisode(e => e.Removed = true);

        // Act
        var exclude = EpisodeSearchIndexEligibility.ShouldExcludeFromSearch(podcast, episode);

        // Assert
        exclude.Should().BeTrue();
    }

    [Fact(DisplayName =
        "Search eligibility: when the podcast is Removed, exclude every episode from Azure Search " +
        "so --reindex-search clears the podcast's documents.")]
    public void excludes_episode_when_podcast_removed()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast(p => p.Removed = true);
        var episode = _fixture.CreateEpisode(e => e.Removed = false);

        // Act
        var exclude = EpisodeSearchIndexEligibility.ShouldExcludeFromSearch(podcast, episode);

        // Assert
        exclude.Should().BeTrue();
    }

    [Fact(DisplayName =
        "Search eligibility: when neither podcast nor episode is Removed, keep the episode " +
        "eligible for Azure Search upload.")]
    public void includes_active_episode()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast(p => p.Removed = false);
        var episode = _fixture.CreateEpisode(e => e.Removed = false);

        // Act
        var exclude = EpisodeSearchIndexEligibility.ShouldExcludeFromSearch(podcast, episode);

        // Assert
        exclude.Should().BeFalse();
    }
}

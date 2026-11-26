using FluentAssertions;
using RedditPodcastPoster.EntitySearchIndexer.D1Search;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using Xunit;

namespace Indexer.Tests.BusinessRules;

public class D1SearchEpisodeParentProjectionRules
{
    private readonly DomainTestFixture _fixture = new();

    [Fact(DisplayName =
        "An episode whose projection matches its podcast is not behind, even after the podcast is saved again, " +
        "because a podcast _ts bump without an indexed change must not make its episodes stale (#1475).")]
    public void matching_projection_is_not_behind()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        podcast.SearchTerms = _fixture.CreateTitle();
        var episode = _fixture.CreateStoredEpisode(podcast);
        episode.SetPodcastProperties(podcast);
        podcast.Timestamp += 1000;

        // Act
        var behind = D1SearchEpisodeParentProjection.IsBehind(podcast, episode);

        // Assert
        behind.Should().BeFalse();
    }

    [Fact(DisplayName =
        "An episode is behind when its podcast was renamed and the projection was not hydrated, because the D1 " +
        "seriesName comes from the live podcast name.")]
    public void rename_is_behind()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var episode = _fixture.CreateStoredEpisode(podcast);
        episode.SetPodcastProperties(podcast);
        podcast.Name = _fixture.CreateTitle();

        // Act
        var behind = D1SearchEpisodeParentProjection.IsBehind(podcast, episode);

        // Assert
        behind.Should().BeTrue();
    }

    [Fact(DisplayName =
        "An episode is behind when its podcast's search terms changed, and blank and missing search terms are the " +
        "same, because D1 indexes publisherSearchTerms from the live podcast and sends a blank as empty.")]
    public void search_terms_change_is_behind_and_blank_equals_missing()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        podcast.SearchTerms = "  ";
        var episode = _fixture.CreateStoredEpisode(podcast);
        episode.SetPodcastProperties(podcast);
        episode.PublisherSearchTerms = null;
        var blankSame = D1SearchEpisodeParentProjection.IsBehind(podcast, episode);
        podcast.SearchTerms = _fixture.CreateTitle();

        // Act
        var changed = D1SearchEpisodeParentProjection.IsBehind(podcast, episode);

        // Assert
        blankSame.Should().BeFalse();
        changed.Should().BeTrue();
    }
}
using FluentAssertions;
using RedditPodcastPoster.EntitySearchIndexer;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Films;
using RedditPodcastPoster.Models.News;
using RedditPodcastPoster.Models.TvShows;
using Xunit;

namespace Indexer.Tests.BusinessRules;

public class CataloguePlayableSearchIndexEligibilityRules
{
    private readonly DomainTestFixture _fixture = new();

    [Fact(DisplayName =
        "Search eligibility: when a TV episode is Removed, exclude it from Azure Search so " +
        "elimination / admin removals delete the search document instead of re-uploading it.")]
    public void excludes_removed_tv_episode()
    {
        // Arrange
        var show = new TvShow(_fixture.CreateTitle()) { Id = _fixture.CreateGuid(), Removed = false };
        var episode = new TvShowEpisode(_fixture.CreateTitle()) { Id = _fixture.CreateGuid(), Removed = true };
        episode.SetTvShowProperties(show);

        // Act
        var exclude = CataloguePlayableSearchIndexEligibility.ShouldExcludeFromSearch(show, episode);

        // Assert
        exclude.Should().BeTrue();
    }

    [Fact(DisplayName =
        "Search eligibility: when the TV show is Removed, exclude every episode from Azure Search " +
        "even if ParentRemoved still lags false after a partial ParentChangeCascade.")]
    public void excludes_tv_episode_when_show_removed_and_denorm_lags()
    {
        // Arrange
        var show = new TvShow(_fixture.CreateTitle()) { Id = _fixture.CreateGuid(), Removed = true };
        var episode = new TvShowEpisode(_fixture.CreateTitle())
        {
            Id = _fixture.CreateGuid(),
            Removed = false,
            ParentRemoved = false
        };
        episode.SetTvShowProperties(show);
        episode.ParentRemoved = false;

        // Act
        var exclude = CataloguePlayableSearchIndexEligibility.ShouldExcludeFromSearch(show, episode);

        // Assert
        exclude.Should().BeTrue();
    }

    [Fact(DisplayName =
        "Search eligibility: when ParentRemoved is true and the live show is not Removed, " +
        "still exclude the TV episode so a restore lag cannot re-upload under a denorm that says removed.")]
    public void excludes_tv_episode_when_parent_removed_denorm_is_set()
    {
        // Arrange
        var show = new TvShow(_fixture.CreateTitle()) { Id = _fixture.CreateGuid(), Removed = false };
        var episode = new TvShowEpisode(_fixture.CreateTitle())
        {
            Id = _fixture.CreateGuid(),
            Removed = false,
            ParentRemoved = true
        };

        // Act
        var exclude = CataloguePlayableSearchIndexEligibility.ShouldExcludeFromSearch(show, episode);

        // Assert
        exclude.Should().BeTrue();
    }

    [Fact(DisplayName =
        "Search eligibility: when neither TV show nor episode is Removed and ParentRemoved is not true, " +
        "keep the episode eligible for Azure Search upload.")]
    public void includes_active_tv_episode()
    {
        // Arrange
        var show = new TvShow(_fixture.CreateTitle()) { Id = _fixture.CreateGuid(), Removed = false };
        var episode = new TvShowEpisode(_fixture.CreateTitle())
        {
            Id = _fixture.CreateGuid(),
            Removed = false,
            ParentRemoved = false
        };
        episode.SetTvShowProperties(show);

        // Act
        var exclude = CataloguePlayableSearchIndexEligibility.ShouldExcludeFromSearch(show, episode);

        // Assert
        exclude.Should().BeFalse();
    }

    [Fact(DisplayName =
        "Search eligibility: when a news organisation is Removed, exclude every report from Azure Search " +
        "even if ParentRemoved still lags false after a partial ParentChangeCascade.")]
    public void excludes_news_report_when_organisation_removed_and_denorm_lags()
    {
        // Arrange
        var organisation = new NewsOrganisation(_fixture.CreateTitle())
        {
            Id = _fixture.CreateGuid(),
            Removed = true
        };
        var report = new NewsReport(_fixture.CreateTitle())
        {
            Id = _fixture.CreateGuid(),
            Removed = false,
            ParentRemoved = false
        };
        report.SetNewsOrganisationProperties(organisation);
        report.ParentRemoved = false;

        // Act
        var exclude = CataloguePlayableSearchIndexEligibility.ShouldExcludeFromSearch(organisation, report);

        // Assert
        exclude.Should().BeTrue();
    }

    [Fact(DisplayName =
        "Search eligibility: when neither organisation nor report is Removed and ParentRemoved is not true, " +
        "keep the news report eligible for Azure Search upload.")]
    public void includes_active_news_report()
    {
        // Arrange
        var organisation = new NewsOrganisation(_fixture.CreateTitle())
        {
            Id = _fixture.CreateGuid(),
            Removed = false
        };
        var report = new NewsReport(_fixture.CreateTitle())
        {
            Id = _fixture.CreateGuid(),
            Removed = false,
            ParentRemoved = false
        };
        report.SetNewsOrganisationProperties(organisation);

        // Act
        var exclude = CataloguePlayableSearchIndexEligibility.ShouldExcludeFromSearch(organisation, report);

        // Assert
        exclude.Should().BeFalse();
    }

    [Fact(DisplayName =
        "Search eligibility (denormalised-only): when ParentRemoved is true and no live parent is passed, " +
        "exclude the playable so call sites without a parent still match the pull filter.")]
    public void denorm_only_excludes_when_parent_removed()
    {
        // Arrange
        var show = new TvShow(_fixture.CreateTitle()) { Id = _fixture.CreateGuid() };
        var episode = new TvShowEpisode(_fixture.CreateTitle()) { Id = _fixture.CreateGuid() };
        episode.SetTvShowProperties(show);
        episode.ParentRemoved = true;

        // Act
        var exclude = CataloguePlayableSearchIndexEligibility.ShouldExcludeFromSearch(episode);

        // Assert
        exclude.Should().BeTrue();
    }

    [Fact(DisplayName =
        "Search eligibility: when a film is Removed, exclude it from Azure Search; a film has no parent.")]
    public void excludes_removed_film()
    {
        // Arrange
        var film = new Film(_fixture.CreateTitle())
        {
            Id = _fixture.CreateGuid(),
            Length = _fixture.CreateDuration(),
            Removed = true
        };

        // Act
        var exclude = CataloguePlayableSearchIndexEligibility.ShouldExcludeFromSearch(film);

        // Assert
        exclude.Should().BeTrue();
    }
}

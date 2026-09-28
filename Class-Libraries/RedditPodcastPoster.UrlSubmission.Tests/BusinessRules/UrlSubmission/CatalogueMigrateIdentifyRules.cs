using FluentAssertions;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.UrlSubmission.Categorisation;
using RedditPodcastPoster.UrlSubmission.Migration;
using Xunit;

namespace RedditPodcastPoster.UrlSubmission.Tests.BusinessRules.UrlSubmission;

public class CatalogueMigrateIdentifyRules
{
    private readonly DomainTestFixture _fixture = new();

    [Fact(DisplayName =
        "A BBC news URL marks the row as a NewsReport candidate that needs an allowlist for apply, " +
        "because News migrate is first and S-008 forbids unattended news-station moves.")]
    public void bbc_news_url_is_a_news_candidate_that_needs_an_allowlist()
    {
        // Arrange
        var signals = new[]
        {
            new SubmitClassificationSignals(new Uri($"https://www.bbc.co.uk/news/{_fixture.CreateGuid():N}"))
        };

        // Act
        var result = CatalogueMigrateIdentify.FromSignals(signals);

        // Assert
        result.ContentKind.Should().Be(SubmitClassification.NewsReport);
        result.RequiresAllowlist.Should().BeTrue();
        result.RequiresCurator.Should().BeFalse();
    }

    [Fact(DisplayName =
        "News beats Film when both signals appear, " +
        "because corpus migrate order is News then Film then TV.")]
    public void news_wins_over_film_when_both_are_present()
    {
        // Arrange
        var signals = new[]
        {
            new SubmitClassificationSignals(
                new Uri($"https://www.netflix.com/watch/{_fixture.CreateGuid():N}"),
                MadeAsFilm: true),
            new SubmitClassificationSignals(new Uri($"https://www.bbc.co.uk/news/{_fixture.CreateGuid():N}"))
        };

        // Act
        var result = CatalogueMigrateIdentify.FromSignals(signals);

        // Assert
        result.ContentKind.Should().Be(SubmitClassification.NewsReport);
        result.RequiresAllowlist.Should().BeTrue();
    }

    [Fact(DisplayName =
        "A series signal beats a made-as-film signal when News is absent, " +
        "because a miniseries or anthology is a TvShow and never a Film.")]
    public void series_beats_film_when_news_is_absent()
    {
        // Arrange
        var signals = new[]
        {
            new SubmitClassificationSignals(
                new Uri($"https://www.netflix.com/watch/{_fixture.CreateGuid():N}"),
                Series: true),
            new SubmitClassificationSignals(
                new Uri($"https://www.netflix.com/watch/{_fixture.CreateGuid():N}"),
                MadeAsFilm: true)
        };

        // Act
        var result = CatalogueMigrateIdentify.FromSignals(signals);

        // Assert
        result.ContentKind.Should().Be(SubmitClassification.TvShowEpisode);
        result.RequiresAllowlist.Should().BeFalse();
    }

    [Fact(DisplayName =
        "A standalone made-as-film signal with no News or series is a Film candidate.")]
    public void film_only_is_a_film_candidate()
    {
        // Arrange
        var signals = new[]
        {
            new SubmitClassificationSignals(
                new Uri($"https://www.netflix.com/watch/{_fixture.CreateGuid():N}"),
                MadeAsFilm: true)
        };

        // Act
        var result = CatalogueMigrateIdentify.FromSignals(signals);

        // Assert
        result.ContentKind.Should().Be(SubmitClassification.Film);
        result.RequiresAllowlist.Should().BeFalse();
        result.RequiresCurator.Should().BeFalse();
    }

    [Fact(DisplayName =
        "A series signal with no News or Film is a TvShowEpisode candidate, " +
        "and it does not need a news allowlist.")]
    public void series_only_is_a_tv_candidate()
    {
        // Arrange
        var signals = new[]
        {
            new SubmitClassificationSignals(
                new Uri($"https://www.netflix.com/watch/{_fixture.CreateGuid():N}"),
                Series: true)
        };

        // Act
        var result = CatalogueMigrateIdentify.FromSignals(signals);

        // Assert
        result.ContentKind.Should().Be(SubmitClassification.TvShowEpisode);
        result.RequiresAllowlist.Should().BeFalse();
        result.RequiresCurator.Should().BeFalse();
    }

    [Fact(DisplayName =
        "Spotify, Apple, or YouTube entertainment URLs stay Episode, " +
        "because those rows are not corpus-migrate candidates.")]
    public void podcast_service_urls_stay_episode()
    {
        // Arrange
        var signals = new[]
        {
            new SubmitClassificationSignals(
                new Uri($"https://open.spotify.com/episode/{_fixture.CreateGuid():N}"),
                PodcastServiceEpisode: true)
        };

        // Act
        var result = CatalogueMigrateIdentify.FromSignals(signals);

        // Assert
        result.ContentKind.Should().Be(SubmitClassification.Episode);
        result.RequiresAllowlist.Should().BeFalse();
        result.RequiresCurator.Should().BeFalse();
    }
}

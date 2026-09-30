using FluentAssertions;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
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
        "Mixed News and Film signals stay Episode and need a curator, " +
        "because a News station candidate requires every classified URL to be News.")]
    public void mixed_news_and_film_signals_are_not_a_news_station()
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
        result.ContentKind.Should().Be(SubmitClassification.Episode);
        result.RequiresAllowlist.Should().BeFalse();
        result.RequiresCurator.Should().BeTrue();
    }

    [Fact(DisplayName =
        "Mixed series and made-as-film URLs stay Episode and need a curator, " +
        "because a migrate candidate requires every classified URL to be the same kind.")]
    public void mixed_series_and_film_signals_are_not_a_tv_station()
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
        result.ContentKind.Should().Be(SubmitClassification.Episode);
        result.RequiresAllowlist.Should().BeFalse();
        result.RequiresCurator.Should().BeTrue();
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

    [Fact(DisplayName =
        "A stored episode whose service URL is a BBC news page is a NewsReport candidate that needs an allowlist, " +
        "because corpus identify reads episode URLs without scraping.")]
    public void stored_bbc_news_episode_url_is_a_news_candidate()
    {
        // Arrange
        var episode = _fixture.CreateEpisode();
        EpisodeServicePresence.Upsert(
            episode,
            "bbcSounds",
            new Uri($"https://www.bbc.co.uk/news/{_fixture.CreateGuid():N}"),
            image: null);

        // Act
        var result = CatalogueMigrateIdentify.FromEpisodes([episode]);

        // Assert
        result.ContentKind.Should().Be(SubmitClassification.NewsReport);
        result.RequiresAllowlist.Should().BeTrue();
    }

    [Fact(DisplayName =
        "A stored Spotify episode stays Episode, " +
        "because podcast-service URLs are not corpus-migrate candidates.")]
    public void stored_spotify_episode_stays_episode()
    {
        // Arrange
        var episode = _fixture.CreateEpisode();
        EpisodeServicePresence.Upsert(
            episode,
            ServiceKeys.Spotify,
            new Uri($"https://open.spotify.com/episode/{_fixture.CreateGuid():N}"),
            image: null);

        // Act
        var result = CatalogueMigrateIdentify.FromEpisodes([episode]);

        // Assert
        result.ContentKind.Should().Be(SubmitClassification.Episode);
        result.RequiresAllowlist.Should().BeFalse();
    }

    [Fact(DisplayName =
        "A podcast with no episode service URLs stays Episode, " +
        "because identify has no signals to classify.")]
    public void episodes_with_no_urls_stay_episode()
    {
        // Arrange
        var episode = _fixture.CreateEpisode();

        // Act
        var result = CatalogueMigrateIdentify.FromEpisodes([episode]);

        // Assert
        result.ContentKind.Should().Be(SubmitClassification.Episode);
        result.RequiresAllowlist.Should().BeFalse();
        result.RequiresCurator.Should().BeFalse();
    }

    [Fact(DisplayName =
        "A stored Spotify URL plus a BBC news URL on the same podcast stays Episode and needs a curator, " +
        "because mixed entertainment and News is not a News station candidate.")]
    public void stored_spotify_plus_bbc_news_is_not_a_news_station()
    {
        // Arrange
        var newsEpisode = _fixture.CreateEpisode();
        EpisodeServicePresence.Upsert(
            newsEpisode,
            "bbcSounds",
            new Uri($"https://www.bbc.co.uk/news/{_fixture.CreateGuid():N}"),
            image: null);
        var spotifyEpisode = _fixture.CreateEpisode();
        EpisodeServicePresence.Upsert(
            spotifyEpisode,
            ServiceKeys.Spotify,
            new Uri($"https://open.spotify.com/episode/{_fixture.CreateGuid():N}"),
            image: null);

        // Act
        var result = CatalogueMigrateIdentify.FromEpisodes([newsEpisode, spotifyEpisode]);

        // Assert
        result.ContentKind.Should().Be(SubmitClassification.Episode);
        result.RequiresAllowlist.Should().BeFalse();
        result.RequiresCurator.Should().BeTrue();
        CatalogueMigrateIdentify.IsNewsReportEpisode(newsEpisode).Should().BeTrue();
        CatalogueMigrateIdentify.IsNewsReportEpisode(spotifyEpisode).Should().BeFalse();
        CatalogueMigrateIdentify.NonNewsEpisodeIds([newsEpisode, spotifyEpisode])
            .Should()
            .Equal(spotifyEpisode.Id);
    }
}

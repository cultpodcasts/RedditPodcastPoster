using AutoFixture;
using FluentAssertions;
using RedditPodcastPoster.BBC.DTOs;
using RedditPodcastPoster.BBC.Extractors;

namespace RedditPodcastPoster.BBC.Tests.BusinessRules;

public class BbcSeriesNameRules
{
    private readonly Fixture _fixture = new();

    [Fact(DisplayName =
        "BBC Sounds titles.primary is the programme/brand when it differs from the episode title, " +
        "so a new series can be named after the show rather than the episode.")]
    public void sounds_primary_is_series_when_distinct_from_episode_title()
    {
        // Arrange
        var series = string.Join(' ', _fixture.CreateMany<string>(2));
        var episode = string.Join(' ', _fixture.CreateMany<string>(3));
        var titles = new Titles { Primary = series, Secondary = episode };

        // Act
        var seriesName = titles.SeriesName;

        // Assert
        titles.Title.Should().Be(episode);
        seriesName.Should().Be(series);
        seriesName.Should().NotBe(titles.Title);
    }

    [Fact(DisplayName =
        "When Sounds titles.primary is the only title, there is no distinct series field, " +
        "so series name is empty and create-from-url may fall back to the episode title.")]
    public void sounds_primary_only_is_not_a_distinct_series()
    {
        // Arrange
        var onlyTitle = string.Join(' ', _fixture.CreateMany<string>(2));
        var titles = new Titles { Primary = onlyTitle };

        // Act
        var seriesName = titles.SeriesName;

        // Assert
        titles.Title.Should().Be(onlyTitle);
        seriesName.Should().BeNull();
    }

    [Fact(DisplayName =
        "Sounds brand container that equals the episode title is a one-off: FromProgrammeBrand returns null " +
        "and must not be forced back to Container.Title for ShowName / podcastName.")]
    public void sounds_brand_equal_to_episode_is_not_forced_as_series()
    {
        // Arrange
        var oneOff = string.Join(' ', _fixture.CreateMany<string>(3));

        // Act
        var fromBrand = BbcSeriesName.FromProgrammeBrand(oneOff, oneOff);

        // Assert
        fromBrand.Should().BeNull();
    }

    [Fact(DisplayName =
        "iPlayer redux episode.title is the programme brand when it differs from the episode subtitle/label, " +
        "because live pages no longer expose og:video:series and subtitle is the episode not the brand.")]
    public void iplayer_redux_brand_is_preferred_over_subtitle()
    {
        // Arrange
        var brand = string.Join(' ', _fixture.CreateMany<string>(2));
        var episodeLabel = string.Join(' ', _fixture.CreateMany<string>(3));

        // Act
        var fromBrand = BbcSeriesName.FromProgrammeBrand(brand, episodeLabel);
        var subtitleMustNotWin = BbcSeriesName.FromDistinctCandidates(episodeLabel, null, episodeLabel);
        var ogFallback = BbcSeriesName.FromDistinctCandidates(episodeLabel, brand);

        // Assert
        fromBrand.Should().Be(brand);
        subtitleMustNotWin.Should().BeNull();
        ogFallback.Should().Be(brand);
    }

    [Fact(DisplayName =
        "iPlayer og:video:series remains a valid brand candidate when redux brand is absent and it differs from the episode title.")]
    public void iplayer_og_series_is_brand_when_distinct()
    {
        // Arrange
        var episodeTitle = string.Join(' ', _fixture.CreateMany<string>(3));
        var brand = string.Join(' ', _fixture.CreateMany<string>(2));

        // Act
        var fromOg = BbcSeriesName.FromDistinctCandidates(episodeTitle, brand, null);
        var sameAsEpisode = BbcSeriesName.FromDistinctCandidates(episodeTitle, episodeTitle, episodeTitle);

        // Assert
        fromOg.Should().Be(brand);
        sameAsEpisode.Should().BeNull();
    }
}

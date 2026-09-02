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
        "iPlayer redux subtitle or og:video:series is the brand when it differs from the episode title.")]
    public void iplayer_subtitle_or_og_series_is_brand_when_distinct()
    {
        // Arrange
        var episodeTitle = string.Join(' ', _fixture.CreateMany<string>(3));
        var brand = string.Join(' ', _fixture.CreateMany<string>(2));

        // Act
        var fromOg = BbcSeriesName.FromDistinctCandidates(episodeTitle, brand, null);
        var fromSubtitle = BbcSeriesName.FromDistinctCandidates(episodeTitle, null, brand);
        var sameAsEpisode = BbcSeriesName.FromDistinctCandidates(episodeTitle, episodeTitle, episodeTitle);

        // Assert
        fromOg.Should().Be(brand);
        fromSubtitle.Should().Be(brand);
        sameAsEpisode.Should().BeNull();
    }
}

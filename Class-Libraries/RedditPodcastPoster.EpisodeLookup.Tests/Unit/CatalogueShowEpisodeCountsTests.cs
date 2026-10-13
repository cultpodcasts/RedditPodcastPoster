using FluentAssertions;
using RedditPodcastPoster.EpisodeLookup;

namespace RedditPodcastPoster.EpisodeLookup.Tests.Unit;

public class CatalogueShowEpisodeCountsTests
{
    [Fact(DisplayName = "Counts are kept per platform and show id and never cross-used between Spotify and Apple.")]
    public void Counts_are_per_platform()
    {
        // Arrange
        var sut = new CatalogueShowEpisodeCounts();

        // Act
        sut.Record(CataloguePlatforms.Spotify, "1603773245", 412);

        // Assert
        sut.TryGet(CataloguePlatforms.Apple, "1603773245", out _).Should().BeFalse();
        sut.TryGet(CataloguePlatforms.Spotify, "1603773245", out var count).Should().BeTrue();
        count.Should().Be(412);
    }

    [Fact(DisplayName = "A negative count or a blank show id is ignored.")]
    public void Invalid_records_are_ignored()
    {
        // Arrange
        var sut = new CatalogueShowEpisodeCounts();

        // Act
        sut.Record(CataloguePlatforms.Spotify, "show", -1);
        sut.Record(CataloguePlatforms.Spotify, " ", 10);

        // Assert
        sut.TryGet(CataloguePlatforms.Spotify, "show", out _).Should().BeFalse();
        sut.TryGet(CataloguePlatforms.Spotify, " ", out _).Should().BeFalse();
    }
}

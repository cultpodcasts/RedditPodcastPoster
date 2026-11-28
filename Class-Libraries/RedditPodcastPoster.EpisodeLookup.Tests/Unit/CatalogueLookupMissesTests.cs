using FluentAssertions;
using RedditPodcastPoster.EpisodeLookup;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;

namespace RedditPodcastPoster.EpisodeLookup.Tests.Unit;

public class CatalogueLookupMissesTests
{
    private readonly DomainTestFixture _fixture = new();

    [Fact(DisplayName = "Record and clear mutations are isolated per podcast id.")]
    public void Mutations_are_per_podcast()
    {
        // Arrange
        var sut = new CatalogueLookupMisses();
        var podcastA = _fixture.CreateGuid();
        var podcastB = _fixture.CreateGuid();
        var episodeId = _fixture.CreateGuid();

        // Act
        sut.RecordMiss(podcastA, episodeId, CataloguePlatforms.Spotify, "a", 1, DomainTestFixture.UtcDaysAgo(0));
        sut.Clear(podcastB, episodeId, CataloguePlatforms.Spotify);

        // Assert
        sut.TryGetChanges(podcastA, out var a).Should().BeTrue();
        a.Should().ContainSingle(c => !c.Clear);
        sut.TryGetChanges(podcastB, out var b).Should().BeTrue();
        b.Should().ContainSingle(c => c.Clear);
    }
}

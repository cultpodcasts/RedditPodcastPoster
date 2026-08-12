using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.SocialPosting.Episodes;

namespace RedditPodcastPoster.PodcastServices.Tests.Episodes;

public class PodcastEpisodePosterTests
{
    private readonly DomainTestFixture _fixture = new();

    [Fact(DisplayName =
        "Reddit posting detached: when PostPodcastEpisode is called, then it returns success without setting Posted, because live Reddit.NET posting is retired pending Devvit.")]
    public async Task post_skips_without_marking_posted()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var episode = _fixture.CreateStoredEpisode(podcast, e => e.Posted = false);
        var podcastEpisode = new PodcastEpisode(podcast, episode);
        var sut = new PodcastEpisodePoster(Mock.Of<ILogger<PodcastEpisodePoster>>());

        // Act
        var result = await sut.PostPodcastEpisode(podcastEpisode);

        // Assert
        result.Success.Should().BeTrue();
        result.Message.Should().Contain("retired");
        episode.Posted.Should().BeFalse();
    }
}

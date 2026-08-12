using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Posting;
using RedditPodcastPoster.Reddit.Episodes;
using RedditPodcastPoster.SocialPosting.Models;

namespace RedditPodcastPoster.Reddit.Tests.Episodes;

public class EpisodePostManagerTests
{
    private readonly DomainTestFixture _fixture = new();

    [Fact(DisplayName =
        "Reddit.NET retirement: when EpisodePostManager.Post is called, then it returns Fail and does not report success, because posting is retired pending Devvit.")]
    public async Task post_returns_fail_and_does_not_succeed()
    {
        // Arrange
        var episodeId = _fixture.CreateGuid().ToString();
        var title = _fixture.CreateTitle();
        var postModel = new PostModel(
            _fixture.CreateTitle(),
            string.Empty,
            string.Empty,
            [
                new EpisodePost(
                    title,
                    null,
                    null,
                    null,
                    "1 Jan 2020",
                    "01:00:00",
                    _fixture.Create<string>(),
                    episodeId,
                    DomainTestFixture.UtcDaysAgo(3),
                    [],
                    null,
                    null)
            ],
            null,
            [],
            []);
        var sut = new EpisodePostManager(Mock.Of<ILogger<EpisodePostManager>>());

        // Act
        var result = await sut.Post(postModel);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("retired");
        result.Message.Should().Contain(episodeId);
        result.Should().BeOfType<ProcessResponse>();
    }
}

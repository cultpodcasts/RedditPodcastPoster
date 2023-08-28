using AutoFixture;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq.AutoMock;
using RedditPodcastPoster.Common.Models;
using RedditPodcastPoster.Common.Reddit;
using RedditPodcastPoster.Common.Text;
using Xunit;

namespace RedditPodcastPoster.Common.Tests.Reddit;

public class RedditPostTitleFactoryTests
{
    private readonly Fixture _fixture;
    private readonly AutoMocker _mocker;

    public RedditPostTitleFactoryTests()
    {
        _fixture = new Fixture();
        _mocker = new AutoMocker();
        _mocker.Use<ITextSanitiser>(new TextSanitiser());
        _mocker.Use(Options.Create(new SubredditSettings {SubredditTitleMaxLength = 300}));
    }

    private RedditPostTitleFactory Sut => _mocker.CreateInstance<RedditPostTitleFactory>();

    [Fact]
    public void ConstructPostTitle_WithA_IsCorrect()
    {
        // arrange
        var postModel = new PostModel(
            new PodcastPost("podcast-title",
                string.Empty,
                string.Empty,
                new[]
                {
                    _fixture
                        .Build<EpisodePost>()
                        .With(x => x.Title, "title-prefix with a title-suffix")
                        .Create()
                }));
        // act
        var result = Sut.ConstructPostTitle(postModel);
        // assert
        result.Should().Contain(" with a ");
    }

    [Fact]
    public void ConstructPostTitle_WithName_IsCorrect()
    {
        // arrange
        var postModel = new PostModel(
            new PodcastPost("podcast-title",
                string.Empty,
                string.Empty,
                new[]
                {
                    _fixture
                        .Build<EpisodePost>()
                        .With(x => x.Title, "title-prefix with Name title-suffix")
                        .Create()
                }));
        // act
        var result = Sut.ConstructPostTitle(postModel);
        // assert
        result.Should().Contain(" w/Name ");
    }
}
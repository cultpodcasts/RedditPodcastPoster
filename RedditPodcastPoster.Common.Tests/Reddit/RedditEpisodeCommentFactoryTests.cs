using System;
using AutoFixture;
using FluentAssertions;
using Moq.AutoMock;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Reddit;
using Xunit;

namespace RedditPodcastPoster.Common.Tests.Reddit;

public class RedditEpisodeCommentFactoryTests
{
    private readonly Fixture _fixture;
    private readonly AutoMocker _mocker;

    public RedditEpisodeCommentFactoryTests()
    {
        _fixture = new Fixture();
        _mocker = new AutoMocker();
    }

    private RedditEpisodeCommentFactory Sut => _mocker.CreateInstance<RedditEpisodeCommentFactory>();

    [Fact]
    public void Post_WithYouTubePreference_IsCorrect()
    {
        // arrange
        var postModel = new PostModel(
            new PodcastPost(_fixture.Create<string>(),
                string.Empty,
                string.Empty,
                new[]
                {
                    _fixture
                        .Build<EpisodePost>()
                        .With(x => x.YouTube, _fixture.Create<Uri>())
                        .With(x => x.Spotify, _fixture.Create<Uri>())
                        .With(x => x.Apple, _fixture.Create<Uri>())
                        .Create()
                },
                Service.YouTube));
        // act
        var comments = Sut.Post(postModel);
        // assert
        comments.Should().NotContain("YouTube:").And.Contain("Spotify:").And.Contain("Apple Podcasts:");
    }


    [Fact]
    public void Post_WithYouTubePreferenceButNoYouTubeUrl_IsCorrect()
    {
        // arrange
        var postModel = new PostModel(
            new PodcastPost(_fixture.Create<string>(),
                string.Empty,
                string.Empty,
                new[]
                {
                    _fixture
                        .Build<EpisodePost>()
                        .With(x => x.YouTube, (Uri?) null)
                        .With(x => x.Spotify, _fixture.Create<Uri>())
                        .With(x => x.Apple, _fixture.Create<Uri>())
                        .Create()
                },
                Service.YouTube));
        // act
        var comments = Sut.Post(postModel);
        // assert
        comments.Should().NotContain("YouTube:").And.NotContain("Spotify:").And.Contain("Apple Podcasts:");
    }

    [Fact]
    public void Post_WithYouTubePreferenceButNoYouTubeUrlOrSpotifyUrl_IsCorrect()
    {
        // arrange
        var postModel = new PostModel(
            new PodcastPost(_fixture.Create<string>(),
                string.Empty,
                string.Empty,
                new[]
                {
                    _fixture
                        .Build<EpisodePost>()
                        .With(x => x.YouTube, (Uri?) null)
                        .With(x => x.Spotify, (Uri?) null)
                        .With(x => x.Apple, _fixture.Create<Uri>())
                        .Create()
                },
                Service.YouTube));
        // act
        var comments = Sut.Post(postModel);
        // assert
        comments.Should().BeEmpty();
    }

    [Fact]
    public void Post_WithSpotifyPreference_IsCorrect()
    {
        // arrange
        var postModel = new PostModel(
            new PodcastPost(_fixture.Create<string>(),
                string.Empty,
                string.Empty,
                new[]
                {
                    _fixture
                        .Build<EpisodePost>()
                        .With(x => x.YouTube, _fixture.Create<Uri>())
                        .With(x => x.Spotify, _fixture.Create<Uri>())
                        .With(x => x.Apple, _fixture.Create<Uri>())
                        .Create()
                },
                Service.Spotify));
        // act
        var comments = Sut.Post(postModel);
        // assert
        comments.Should().Contain("YouTube:").And.NotContain("Spotify:").And.Contain("Apple Podcasts:");
    }

    [Fact]
    public void Post_WithApplePreference_IsCorrect()
    {
        // arrange
        var postModel = new PostModel(
            new PodcastPost(_fixture.Create<string>(),
                string.Empty,
                string.Empty,
                new[]
                {
                    _fixture
                        .Build<EpisodePost>()
                        .With(x => x.YouTube, _fixture.Create<Uri>())
                        .With(x => x.Spotify, _fixture.Create<Uri>())
                        .With(x => x.Apple, _fixture.Create<Uri>())
                        .Create()
                },
                Service.Apple));
        // act
        var comments = Sut.Post(postModel);
        // assert
        comments.Should().Contain("YouTube:").And.Contain("Spotify:").And.NotContain("Apple Podcasts:");
    }
}
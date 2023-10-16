using AutoFixture;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq.AutoMock;
using RedditPodcastPoster.Common.KnownTerms;
using RedditPodcastPoster.Common.Models;
using RedditPodcastPoster.Common.Reddit;
using RedditPodcastPoster.Common.Text;
using RedditPodcastPoster.Models;
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
        _mocker.GetMock<IKnownTermsProvider>().Setup(x => x.GetKnownTerms()).Returns(new KnownTerms.KnownTerms());
        _mocker.Use<ITextSanitiser>(_mocker.CreateInstance<TextSanitiser>());
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
                }, _fixture.Create<Service?>()));
        // act
        var result = Sut.ConstructPostTitle(postModel);
        // assert
        result.Should().Contain(" with a ");
    }

    [Fact]
    public void ConstructPostTitle_WithLowerCaseTitle_IsCorrect()
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
                        .With(x => x.Title, "episode title")
                        .Create()
                },
                _fixture.Create<Service?>()));
        // act
        var result = Sut.ConstructPostTitle(postModel);
        // assert
        result.Should().Contain("Episode Title");
    }

    [Fact]
    public void ConstructPostTitle_WithAllUpperText_IsCorrect()
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
                        .With(x => x.Title, "Episode title UPPER TEXT")
                        .Create()
                },
                _fixture.Create<Service?>()));
        // act
        var result = Sut.ConstructPostTitle(postModel);
        // assert
        result.Should().Contain("Episode Title Upper Text");
    }

    [Fact]
    public void ConstructPostTitle_LowerCasePodCastTitle_IsCorrect()
    {
        // arrange
        var originalTitle = "podcast title";
        var postModel = new PostModel(
            new PodcastPost(originalTitle,
                string.Empty,
                string.Empty,
                new[]
                {
                    _fixture
                        .Build<EpisodePost>()
                        .With(x => x.Title, "title-prefix With Name title-suffix")
                        .Create()
                },
                _fixture.Create<Service?>()));
        // act
        var result = Sut.ConstructPostTitle(postModel);
        // assert
        result.Should().Contain(originalTitle);
    }

    [Theory]
    [InlineData(" - ")]
    [InlineData(" ")]
    [InlineData("-")]
    public void ConstructPostTitle_TitleBeginningWithNonWordCharacter_IsCorrect(string prefix)
    {
        // arrange
        var postModel = new PostModel(
            new PodcastPost("podcast title",
                string.Empty,
                string.Empty,
                new[]
                {
                    _fixture
                        .Build<EpisodePost>()
                        .With(x => x.Title, $"{prefix}Proper Title")
                        .Create()
                },
                _fixture.Create<Service?>()));
        // act
        var result = Sut.ConstructPostTitle(postModel);
        // assert
        result.Should().StartWith("\"Proper Title");
    }
}
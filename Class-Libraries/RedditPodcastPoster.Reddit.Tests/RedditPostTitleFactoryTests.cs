using Microsoft.Extensions.Options;
using AutoFixture;
using FluentAssertions;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.DependencyInjection;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Models.Posting;
using RedditPodcastPoster.Models.TitleCasing;
using RedditPodcastPoster.Reddit.Configuration;
using RedditPodcastPoster.Reddit.Factories;
using RedditPodcastPoster.Text.Models;
using RedditPodcastPoster.Text.Sanitisers;
using RedditPodcastPoster.Text.TitleCasing;

namespace RedditPodcastPoster.Reddit.Tests;

public class RedditPostTitleFactoryTests
{
    private readonly Fixture _fixture;
    private readonly AutoMocker _mocker;

    public RedditPostTitleFactoryTests()
    {
        _fixture = new Fixture();
        _mocker = new AutoMocker();

        var rules = new TitleCasingRulesProvider(
            new Dictionary<string, LanguageTitleCasingRulesDocument>(StringComparer.OrdinalIgnoreCase)
            {
                ["en"] = LanguageTitleCasingRulesDocument.CreateEnglishDefault(
                    LowerCaseTerms.DefaultEnglishWords)
            });
        var rulesInstance = _mocker.GetMock<IAsyncInstance<ITitleCasingRulesProvider>>();
        rulesInstance.Setup(x => x.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync(rules);
        _mocker.Use(rulesInstance.Object);

        _mocker.Use<ITextSanitiser>(_mocker.CreateInstance<TextSanitiser>());
        _mocker.Use(Options.Create(new SubredditSettings { SubredditTitleMaxLength = 300 }));
    }

    private RedditPostTitleFactory Sut => _mocker.CreateInstance<RedditPostTitleFactory>();

    [Fact(DisplayName = "ConstructPostTitle_WithA_IsCorrect")]
    public async Task ConstructPostTitle_WithA_IsCorrect()
    {
        // arrange
        var postModel = new PostModel(
            "podcast-title",
            string.Empty,
            string.Empty,
            [
                _fixture
                    .Build<EpisodePost>()
                    .With(x => x.Title, "title-prefix with a title-suffix")
                    .Create()
            ], _fixture.Create<Service?>(), [], []);
        // act
        var result = await Sut.ConstructPostTitle(postModel);
        // assert
        result.Should().Contain(" with a ");
    }

    [Fact(DisplayName = "ConstructPostTitle_WithLowerCaseTitle_IsCorrect")]
    public async Task ConstructPostTitle_WithLowerCaseTitle_IsCorrect()
    {
        // arrange
        var postModel = new PostModel(
            "podcast-title",
            string.Empty,
            string.Empty,
            [
                _fixture
                    .Build<EpisodePost>()
                    .With(x => x.Title, "episode title")
                    .Create()
            ],
            _fixture.Create<Service?>(), [], []);
        // act
        var result = await Sut.ConstructPostTitle(postModel);
        // assert
        result.Should().Contain("Episode Title");
    }

    [Fact(DisplayName = "ConstructPostTitle_WithAllUpperText_IsCorrect")]
    public async Task ConstructPostTitle_WithAllUpperText_IsCorrect()
    {
        // arrange
        var postModel = new PostModel(
            "podcast-title",
            string.Empty,
            string.Empty,
            [
                _fixture
                    .Build<EpisodePost>()
                    .With(x => x.Title, "Episode title UPPER TEXT")
                    .Create()
            ],
            _fixture.Create<Service?>(), [], []);
        // act
        var result = await Sut.ConstructPostTitle(postModel);
        // assert
        result.Should().Contain("Episode Title Upper Text");
    }

    [Fact(DisplayName = "ConstructPostTitle_LowerCasePodCastTitle_IsCorrect")]
    public async Task ConstructPostTitle_LowerCasePodCastTitle_IsCorrect()
    {
        // arrange
        var originalTitle = "podcast title";
        var postModel = new PostModel(
            originalTitle,
            string.Empty,
            string.Empty,
            [
                _fixture
                    .Build<EpisodePost>()
                    .With(x => x.Title, "title-prefix With Name title-suffix")
                    .Create()
            ],
            _fixture.Create<Service?>(), [], []);
        // act
        var result = await Sut.ConstructPostTitle(postModel);
        // assert
        result.Should().Contain(originalTitle);
    }

    [Theory(DisplayName = "ConstructPostTitle_TitleBeginningWithNonWordCharacter_IsCorrect")]
    [InlineData(" - ")]
    [InlineData(" ")]
    [InlineData("-")]
    public async Task ConstructPostTitle_TitleBeginningWithNonWordCharacter_IsCorrect(string prefix)
    {
        // arrange
        var postModel = new PostModel(
            "podcast title",
            string.Empty,
            string.Empty,
            [
                _fixture
                    .Build<EpisodePost>()
                    .With(x => x.Title, $"{prefix}Proper Title")
                    .Create()
            ],
            _fixture.Create<Service?>(), [], []);
        // act
        var result = await Sut.ConstructPostTitle(postModel);
        // assert
        result.Should().StartWith("\"Proper Title");
    }
}

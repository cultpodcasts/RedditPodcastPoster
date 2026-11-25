using System.Text.RegularExpressions;
using AutoFixture;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Models.Posting;
using RedditPodcastPoster.Text.Sanitisers;

namespace RedditPodcastPoster.Text.Tests.BusinessRules;

/// <summary>
/// A parent's title regex shares the description-regex match timeout. A match that exceeds it leaves the raw title
/// and records the parent id and pattern length, so homepage publish and posting continue (#1383).
/// </summary>
public class TitleRegexTimeoutRules
{
    private const string CatastrophicPattern = @"^(?<title>(x+x+)+y)$";

    private readonly Fixture _fixture = new();
    private readonly AutoMocker _mocker = new();

    public TitleRegexTimeoutRules()
    {
        TitleCasingTestSupport.UseDefaultEnglishRules(_mocker);
    }

    private TextSanitiser Sut => _mocker.CreateInstance<TextSanitiser>();

    [Fact(DisplayName =
        "Plain English rule: when a parent title regex is built, then its match timeout is the shared two-second " +
        "description-regex timeout, because a pathological pattern must not hang homepage publish or posting.")]
    public void title_regex_uses_the_shared_match_timeout()
    {
        // Arrange
        var pattern = $@"^(?'title'{_fixture.Create<string>()})$";

        // Act
        var regex = Podcast.CreateTitleRegex(pattern);

        // Assert
        regex.Should().NotBeNull();
        regex!.MatchTimeout.Should().Be(DescriptionRegexTokens.MatchTimeout);
    }

    [Fact(DisplayName =
        "Plain English rule: when a title regex match exceeds its timeout, then the raw title is kept and the " +
        "warning names the parent id and pattern length, because the pattern and title must not be logged.")]
    public void timed_out_title_match_keeps_the_raw_title()
    {
        // Arrange
        var title = new string('x', 40);
        var parentId = _fixture.Create<Guid>();
        var pathological = new Regex(CatastrophicPattern, RegexOptions.None, TimeSpan.FromMilliseconds(1));

        // Act
        var result = Sut.ExtractTitle(title, pathological, parentId);

        // Assert
        result.Should().Be(title);
        _mocker.GetMock<ILogger<TextSanitiser>>().Verify(x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) =>
                    WarningNamesParentAndLength(v.ToString(), parentId, pathological.ToString().Length) &&
                    !v.ToString()!.Contains(CatastrophicPattern, StringComparison.Ordinal)),
                It.Is<Exception?>(ex => ex is RegexMatchTimeoutException),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact(DisplayName =
        "Plain English rule: when a post is built from a title regex that cannot finish matching, then posting " +
        "still produces the raw title, because a timeout must not fail the post.")]
    public async Task posting_keeps_the_raw_title_when_the_title_regex_times_out()
    {
        // Arrange
        var title = new string('x', 30);
        var episode = new EpisodePost(
            title,
            null,
            null,
            null,
            _fixture.Create<string>(),
            _fixture.Create<string>(),
            _fixture.Create<string>(),
            _fixture.Create<string>(),
            _fixture.Create<DateTime>(),
            [],
            null,
            null);
        var postModel = new PostModel(
            _fixture.Create<string>(),
            CatastrophicPattern,
            string.Empty,
            [episode],
            null,
            [],
            []);

        // Act
        var result = await Sut.SanitiseTitle(postModel);

        // Assert
        result.Should().Be("X" + title[1..]);
        postModel.BundledPartNumbers.Should().BeEmpty();
    }

    private static bool WarningNamesParentAndLength(string? message, Guid parentId, int patternLength) =>
        message != null &&
        message.Contains(parentId.ToString(), StringComparison.OrdinalIgnoreCase) &&
        message.Contains(patternLength.ToString(), StringComparison.Ordinal);
}

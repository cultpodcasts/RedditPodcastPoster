using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.PodcastServices.YouTube.CandidatePreference;

namespace RedditPodcastPoster.PodcastServices.YouTube.Tests.CandidatePreference;

public class YouTubeCandidatePreferenceTests
{
    private readonly AutoMocker _mocker = new();
    private readonly YouTubeCandidatePreferenceOptions _options = new();

    public YouTubeCandidatePreferenceTests()
    {
        _mocker.Use(Options.Create(_options));
        _mocker.Use<ILogger<YouTubeCandidatePreference>>(NullLogger<YouTubeCandidatePreference>.Instance);
    }

    private YouTubeCandidatePreference Sut() => _mocker.CreateInstance<YouTubeCandidatePreference>();

    private void Motion(string id, ThumbnailMotion motion) =>
        _mocker.GetMock<IYouTubeThumbnailMotionDetector>()
            .Setup(x => x.DetectAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(motion);

    [Fact(DisplayName = "With no contenders, nothing is fetched and the finder's choice stands.")]
    public async Task no_contenders()
    {
        // Arrange
        var sut = Sut();

        // Act
        var chosen = await sut.ChooseAsync(new YouTubeCandidate("a", "A", null), []);

        // Assert
        chosen.Should().Be("a");
        _mocker.GetMock<IYouTubeThumbnailMotionDetector>().VerifyNoOtherCalls();
    }

    [Fact(DisplayName = "A static, 📱-titled choice loses to a moving plain contender.")]
    public async Task double_penalty_loses()
    {
        // Arrange
        Motion("a", ThumbnailMotion.Static);
        Motion("b", ThumbnailMotion.Moving);
        var sut = Sut();

        // Act
        var chosen = await sut.ChooseAsync(
            new YouTubeCandidate("a", "X \U0001F4F1", null),
            [new YouTubeCandidate("b", "X", null)]);

        // Assert
        chosen.Should().Be("b");
    }

    [Fact(DisplayName = "The less-penalised contender wins among several.")]
    public async Task least_penalised_wins()
    {
        // Arrange
        Motion("a", ThumbnailMotion.Static);
        Motion("b", ThumbnailMotion.Static);
        Motion("c", ThumbnailMotion.Moving);
        var sut = Sut();

        // Act
        var chosen = await sut.ChooseAsync(
            new YouTubeCandidate("a", "X \U0001F4F1", null),
            [new YouTubeCandidate("b", "X", null), new YouTubeCandidate("c", "X \U0001F4F1", null)]);

        // Assert
        chosen.Should().Be("b", "a carries both penalties; b and c carry one each, so the tie between them goes to list order");
    }

    [Fact(DisplayName = "Penalties come from options: a larger static penalty outweighs the title penalty.")]
    public async Task penalties_are_configurable()
    {
        // Arrange
        _options.StaticImagePenalty = 50;
        Motion("a", ThumbnailMotion.Static);
        Motion("b", ThumbnailMotion.Moving);
        var sut = Sut();

        // Act
        var chosen = await sut.ChooseAsync(
            new YouTubeCandidate("a", "X", null),
            [new YouTubeCandidate("b", "X \U0001F4F1", null)]);

        // Assert
        chosen.Should().Be("b");
    }

    [Theory(DisplayName = "Penalised title tokens match anywhere in the title, ordinally.")]
    [InlineData("EL-ROI CANNOT SAVE ANYONE \U0001F4F1", true)]
    [InlineData("\U0001F4F1EL-ROI", true)]
    [InlineData("EL-ROI CANNOT SAVE ANYONE", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void penalised_token_detection(string? title, bool expected)
    {
        // Arrange
        var options = _options;

        // Act
        var detected = YouTubeCandidatePreference.HasPenalisedToken(title, options);

        // Assert
        detected.Should().Be(expected);
    }

    [Fact(DisplayName = "Normalised titles drop penalised tokens, collapse whitespace and lower-case.")]
    public void normalise()
    {
        // Arrange
        const string title = "EL-ROI  CANNOT SAVE ANYONE \U0001F4F1";

        // Act
        var normalised = YouTubeCandidatePreferenceApplier.Normalise(title, _options);

        // Assert
        normalised.Should().Be("el-roi cannot save anyone");
    }

    [Fact(DisplayName = "Default options are valid; out-of-range values fail validation.")]
    public void options_validation()
    {
        // Arrange
        var validator = new YouTubeCandidatePreferenceOptionsValidator();
        var defaults = new YouTubeCandidatePreferenceOptions();
        var noContenders = new YouTubeCandidatePreferenceOptions { MaxContenders = 0 };
        var negativeThreshold = new YouTubeCandidatePreferenceOptions { StaticImageThreshold = -1 };
        var nullTokens = new YouTubeCandidatePreferenceOptions { PenalisedTitleTokens = null! };
        var tinyThumbnailCap = new YouTubeCandidatePreferenceOptions
        {
            MaxThumbnailBytes = YouTubeCandidatePreferenceOptions.MinimumMaxThumbnailBytes - 1
        };
        var hugeThumbnailCap = new YouTubeCandidatePreferenceOptions
        {
            MaxThumbnailBytes = YouTubeCandidatePreferenceOptions.MaximumMaxThumbnailBytes + 1
        };
        var noRedirects = new YouTubeCandidatePreferenceOptions { MaxThumbnailRedirects = 0 };

        // Act
        var defaultResult = validator.Validate(null, defaults);
        var noContendersResult = validator.Validate(null, noContenders);
        var negativeThresholdResult = validator.Validate(null, negativeThreshold);
        var nullTokensResult = validator.Validate(null, nullTokens);
        var tinyThumbnailCapResult = validator.Validate(null, tinyThumbnailCap);
        var hugeThumbnailCapResult = validator.Validate(null, hugeThumbnailCap);
        var noRedirectsResult = validator.Validate(null, noRedirects);

        // Assert
        defaultResult.Succeeded.Should().BeTrue();
        defaults.MaxThumbnailBytes.Should().Be(YouTubeCandidatePreferenceOptions.DefaultMaxThumbnailBytes);
        defaults.MaxThumbnailRedirects.Should().Be(YouTubeCandidatePreferenceOptions.DefaultMaxThumbnailRedirects);
        noContendersResult.Failed.Should().BeTrue();
        negativeThresholdResult.Failed.Should().BeTrue();
        nullTokensResult.Failed.Should().BeTrue();
        tinyThumbnailCapResult.Failed.Should().BeTrue();
        hugeThumbnailCapResult.Failed.Should().BeTrue();
        noRedirectsResult.Failed.Should().BeTrue();
    }

    [Fact(DisplayName = "Frame difference is zero for identical frames and 255 for black vs white.")]
    public void frame_difference()
    {
        // Arrange
        var black = new ThumbnailFrame(4, 4, new byte[48]);
        var white = new ThumbnailFrame(4, 4, Enumerable.Repeat((byte)255, 48).ToArray());
        var mismatched = new ThumbnailFrame(2, 2, new byte[12]);

        // Act
        var identical = ThumbnailFrame.MeanAbsoluteDifference(black, black);
        var opposite = ThumbnailFrame.MeanAbsoluteDifference(black, white);
        var deviation = black.PixelStandardDeviation();
        var act = () => ThumbnailFrame.MeanAbsoluteDifference(black, mismatched);

        // Assert
        identical.Should().Be(0);
        opposite.Should().Be(255);
        deviation.Should().Be(0);
        act.Should().Throw<ArgumentException>();
    }
}

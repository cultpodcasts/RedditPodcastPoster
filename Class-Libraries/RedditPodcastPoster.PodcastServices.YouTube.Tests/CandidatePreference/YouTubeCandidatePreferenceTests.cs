using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using RedditPodcastPoster.PodcastServices.YouTube.CandidatePreference;

namespace RedditPodcastPoster.PodcastServices.YouTube.Tests.CandidatePreference;

public class YouTubeCandidatePreferenceTests
{
    private readonly Mock<IYouTubeThumbnailMotionDetector> _motion = new();
    private readonly YouTubeCandidatePreferenceOptions _options = new();

    private YouTubeCandidatePreference Sut() =>
        new(_motion.Object, Options.Create(_options), NullLogger<YouTubeCandidatePreference>.Instance);

    private void Motion(string id, ThumbnailMotion motion) =>
        _motion.Setup(x => x.DetectAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(motion);

    [Fact(DisplayName = "With no contenders, nothing is fetched and the finder's choice stands.")]
    public async Task no_contenders()
    {
        (await Sut().ChooseAsync(new YouTubeCandidate("a", "A", null), [])).Should().Be("a");
        _motion.VerifyNoOtherCalls();
    }

    [Fact(DisplayName = "A static, 📱-titled choice loses to a moving plain contender.")]
    public async Task double_penalty_loses()
    {
        Motion("a", ThumbnailMotion.Static);
        Motion("b", ThumbnailMotion.Moving);
        (await Sut().ChooseAsync(new YouTubeCandidate("a", "X \U0001F4F1", null), [new YouTubeCandidate("b", "X", null)]))
            .Should().Be("b");
    }

    [Fact(DisplayName = "The less-penalised contender wins among several.")]
    public async Task least_penalised_wins()
    {
        Motion("a", ThumbnailMotion.Static);
        Motion("b", ThumbnailMotion.Static);
        Motion("c", ThumbnailMotion.Moving);
        (await Sut().ChooseAsync(
                new YouTubeCandidate("a", "X \U0001F4F1", null),
                [new YouTubeCandidate("b", "X", null), new YouTubeCandidate("c", "X \U0001F4F1", null)]))
            .Should().Be("b", "a carries both penalties; b and c carry one each, so the tie between them goes to list order");
    }

    [Fact(DisplayName = "Penalties come from options: a larger static penalty outweighs the title penalty.")]
    public async Task penalties_are_configurable()
    {
        _options.StaticImagePenalty = 50;
        Motion("a", ThumbnailMotion.Static);
        Motion("b", ThumbnailMotion.Moving);
        (await Sut().ChooseAsync(new YouTubeCandidate("a", "X", null), [new YouTubeCandidate("b", "X \U0001F4F1", null)]))
            .Should().Be("b");
    }

    [Theory(DisplayName = "Penalised title tokens match anywhere in the title, ordinally.")]
    [InlineData("EL-ROI CANNOT SAVE ANYONE \U0001F4F1", true)]
    [InlineData("\U0001F4F1EL-ROI", true)]
    [InlineData("EL-ROI CANNOT SAVE ANYONE", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void penalised_token_detection(string? title, bool expected) =>
        YouTubeCandidatePreference.HasPenalisedToken(title, _options).Should().Be(expected);

    [Fact(DisplayName = "Normalised titles drop penalised tokens, collapse whitespace and lower-case.")]
    public void normalise() =>
        YouTubeCandidatePreferenceApplier.Normalise("EL-ROI  CANNOT SAVE ANYONE \U0001F4F1", _options)
            .Should().Be("el-roi cannot save anyone");

    [Fact(DisplayName = "Default options are valid; out-of-range values fail validation.")]
    public void options_validation()
    {
        var validator = new YouTubeCandidatePreferenceOptionsValidator();
        validator.Validate(null, new YouTubeCandidatePreferenceOptions()).Succeeded.Should().BeTrue();
        validator.Validate(null, new YouTubeCandidatePreferenceOptions { MaxContenders = 0 }).Failed.Should().BeTrue();
        validator.Validate(null, new YouTubeCandidatePreferenceOptions { StaticImageThreshold = -1 }).Failed.Should().BeTrue();
        validator.Validate(null, new YouTubeCandidatePreferenceOptions { PenalisedTitleTokens = null! }).Failed.Should().BeTrue();
    }

    [Fact(DisplayName = "Frame difference is zero for identical frames and 255 for black vs white.")]
    public void frame_difference()
    {
        var black = new ThumbnailFrame(4, 4, new byte[48]);
        var white = new ThumbnailFrame(4, 4, Enumerable.Repeat((byte)255, 48).ToArray());
        ThumbnailFrame.MeanAbsoluteDifference(black, black).Should().Be(0);
        ThumbnailFrame.MeanAbsoluteDifference(black, white).Should().Be(255);
        black.PixelStandardDeviation().Should().Be(0);
        var act = () => ThumbnailFrame.MeanAbsoluteDifference(black, new ThumbnailFrame(2, 2, new byte[12]));
        act.Should().Throw<ArgumentException>();
    }
}
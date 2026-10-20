using System.Text.RegularExpressions;
using AutoFixture;
using FluentAssertions;
using Moq;
using RedditPodcastPoster.ContentPublisher.Models;
using RedditPodcastPoster.ContentPublisher.Publishers;
using RedditPodcastPoster.Models.Subjects;
using RedditPodcastPoster.Text.Sanitisers;

namespace RedditPodcastPoster.ContentPublisher.Tests.BusinessRules;

public class HomepageDescriptionRegexRules
{
    private const string TitlePrefixPreset = "^\\s*{episode-title}\\s*[-\u2013\u2014:|]?\\s*(?<body>.*)$";

    private readonly Fixture _fixture = new();
    private readonly Mock<ITextSanitiser> _textSanitiser = new();
    private string _sanitisedTitle = string.Empty;

    public HomepageDescriptionRegexRules()
    {
        _textSanitiser
            .Setup(x => x.SanitiseTitle(It.IsAny<string>(), It.IsAny<Regex?>(), It.IsAny<string[]>(),
                It.IsAny<string[]>(), It.IsAny<string?>()))
            .ReturnsAsync(() => _sanitisedTitle);
        _textSanitiser
            .Setup(x => x.SanitiseDescription(It.IsAny<string>(), It.IsAny<Regex?>()))
            .Returns((string description, Regex? regex) =>
                regex?.Match(description) is { Success: true } match ? match.Groups["body"].Value : description);
    }

    private string Word() => "w" + _fixture.Create<Guid>().ToString("N")[..8];

    [Fact(DisplayName =
        "Homepage: the description regex {episode-title} token resolves to the raw episode title, not the sanitised " +
        "title, because the feed description starts with the raw title.")]
    public async Task episode_title_token_uses_raw_title()
    {
        // Arrange
        var rawTitle = $"{Word()} {Word()}: {Word()} \u2014 {Word()}";
        var body = $"{Word()} {Word()}. {Word()}.";
        _sanitisedTitle = $"{Word()} {Word()}";
        var podcastResult = new PodcastResult
        {
            PodcastName = Word(),
            EpisodeTitle = rawTitle,
            EpisodeDescription = rawTitle + body,
            DescriptionRegex = TitlePrefixPreset
        };

        // Act
        var result = await HomepagePublisher.SanitiseResult(podcastResult, Array.Empty<Subject>(),
            _textSanitiser.Object);

        // Assert
        result.EpisodeTitle.Should().Be(_sanitisedTitle);
        result.EpisodeDescription.Should().Be(body);
    }

    [Fact(DisplayName =
        "Homepage: when the description starts with the sanitised title rather than the raw title, the " +
        "{episode-title} token does not strip it, because the token is the raw title.")]
    public async Task episode_title_token_does_not_use_sanitised_title()
    {
        // Arrange
        var rawTitle = $"{Word()} {Word()}: {Word()}";
        _sanitisedTitle = $"{Word()} {Word()}";
        var description = _sanitisedTitle + " " + Word();
        var podcastResult = new PodcastResult
        {
            PodcastName = Word(),
            EpisodeTitle = rawTitle,
            EpisodeDescription = description,
            DescriptionRegex = TitlePrefixPreset
        };

        // Act
        var result = await HomepagePublisher.SanitiseResult(podcastResult, Array.Empty<Subject>(),
            _textSanitiser.Object);

        // Assert
        result.EpisodeDescription.Should().Be(description);
    }
}

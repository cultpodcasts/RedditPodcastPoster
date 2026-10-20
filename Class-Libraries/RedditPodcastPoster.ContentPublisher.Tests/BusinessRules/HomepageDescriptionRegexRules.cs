using System.Text.RegularExpressions;
using AutoFixture;
using FluentAssertions;
using Moq;
using RedditPodcastPoster.ContentPublisher.Models;
using RedditPodcastPoster.ContentPublisher.Publishers;
using RedditPodcastPoster.Text.Sanitisers;

namespace RedditPodcastPoster.ContentPublisher.Tests.BusinessRules;

public class HomepageDescriptionRegexRules
{
    private const string TitlePrefixPreset = "^\\s*{episode-title}\\s*[-\u2013\u2014:|]?\\s*(?<body>.*)$";

    private readonly Fixture _fixture = new();
    private readonly Mock<ITextSanitiser> _textSanitiser = new();

    public HomepageDescriptionRegexRules()
    {
        _textSanitiser
            .Setup(x => x.SanitiseDescription(It.IsAny<string>(), It.IsAny<Regex?>()))
            .Returns((string description, Regex? regex) =>
                regex?.Match(description) is { Success: true } match ? match.Groups["body"].Value : description);
    }

    private string Word() => "w" + _fixture.Create<Guid>().ToString("N")[..8];

    [Fact(DisplayName =
        "Homepage: the description regex {episode-title} token resolves to the raw episode title, not the sanitised " +
        "title, because the feed description starts with the raw title.")]
    public void episode_title_token_uses_raw_title()
    {
        // Arrange
        var rawTitle = $"{Word()} {Word()}: {Word()} \u2014 {Word()}";
        var body = $"{Word()} {Word()}. {Word()}.";
        var podcastResult = new PodcastResult
        {
            PodcastName = Word(),
            EpisodeTitle = $"{Word()} {Word()}",
            EpisodeDescription = rawTitle + body,
            DescriptionRegex = TitlePrefixPreset
        };

        // Act
        var result = HomepagePublisher.SanitiseDescription(podcastResult, rawTitle, _textSanitiser.Object);

        // Assert
        result.Should().Be(body);
    }

    [Fact(DisplayName =
        "Homepage: when the description starts with the sanitised title rather than the raw title, the " +
        "{episode-title} token does not strip it, because the token is the raw title.")]
    public void episode_title_token_does_not_use_sanitised_title()
    {
        // Arrange
        var rawTitle = $"{Word()} {Word()}: {Word()}";
        var sanitisedTitle = $"{Word()} {Word()}";
        var description = sanitisedTitle + " " + Word();
        var podcastResult = new PodcastResult
        {
            PodcastName = Word(),
            EpisodeTitle = sanitisedTitle,
            EpisodeDescription = description,
            DescriptionRegex = TitlePrefixPreset
        };

        // Act
        var result = HomepagePublisher.SanitiseDescription(podcastResult, rawTitle, _textSanitiser.Object);

        // Assert
        result.Should().Be(description);
    }
}

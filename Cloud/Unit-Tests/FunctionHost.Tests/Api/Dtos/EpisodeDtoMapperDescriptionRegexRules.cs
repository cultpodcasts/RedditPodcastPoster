using System.Text.RegularExpressions;
using FluentAssertions;
using Moq;
using Moq.AutoMock;
using Api.Dtos.Mapping;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Text.Sanitisers;
using Xunit;
using Podcast = RedditPodcastPoster.Models.Podcasts.Podcast;
using Subject = RedditPodcastPoster.Models.Subjects.Subject;

namespace FunctionHost.Tests.Api.Dtos;

public class EpisodeDtoMapperDescriptionRegexRules
{
    private const string TitlePrefixPreset = "^\\s*{episode-title}\\s*[-\u2013\u2014:|]?\\s*(?<body>.*)$";

    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();

    public EpisodeDtoMapperDescriptionRegexRules()
    {
        _mocker.GetMock<ITextSanitiser>()
            .Setup(x => x.SanitiseTitle(It.IsAny<string>(), It.IsAny<Regex?>(), It.IsAny<string[]>(),
                It.IsAny<string[]>(), It.IsAny<string?>()))
            .ReturnsAsync((string title, Regex? _, string[] _, string[] _, string? _) => title);
        _mocker.GetMock<ITextSanitiser>()
            .Setup(x => x.SanitiseDescription(It.IsAny<string>(), It.IsAny<Regex?>()))
            .Returns((string description, Regex? regex) =>
                regex?.Match(description) is { Success: true } match ? match.Groups["body"].Value : description);
    }

    [Fact(DisplayName =
        "Api episode DTO: when the podcast description regex uses {episode-title}, the display description has the " +
        "episode's title prefix stripped, while the raw description is unchanged.")]
    public async Task display_description_strips_episode_title_prefix()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast(p => p.DescriptionRegex = TitlePrefixPreset);
        var title = _fixture.CreateTitle();
        var body = _fixture.CreateTitle();
        var episode = _fixture.BuildEpisode()
            .WithPodcast(podcast)
            .Customize(e =>
            {
                e.Title = title;
                e.Description = title + body;
                e.Guests = null;
            })
            .Create();
        var sut = _mocker.CreateInstance<EpisodeDtoMapper>();

        // Act
        var dto = await sut.ToDto(episode, podcast, Array.Empty<Subject>());

        // Assert
        dto.DisplayDescription.Should().Be(body);
        dto.Description.Should().Be(title + body);
    }
}

using FluentAssertions;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Models.Subjects;
using RedditPodcastPoster.Persistence.Abstractions.Providers;
using RedditPodcastPoster.Subjects.Services;
using RedditPodcastPoster.Text.Sanitisers;

namespace RedditPodcastPoster.Subjects.Tests.BusinessRules;

public class SubjectDescriptionRegexRules
{
    private const string PodcastNamePrefixPattern = @"^\s*{podcast-name}\s*(?<body>.*)$";

    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private List<Subject> _subjects = [];

    public SubjectDescriptionRegexRules()
    {
        _mocker.GetMock<ISubjectsProvider>()
            .Setup(x => x.GetAll())
            .Returns(() => _subjects.ToAsyncEnumerable());
        // Behaves like TextSanitiser for plain-text descriptions: tokens resolve, ${body} replaces the description.
        _mocker.GetMock<ITextSanitiser>()
            .Setup(x => x.ExtractDescription(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(),
                It.IsAny<string?>()))
            .Returns((string description, string pattern, string? title, string? podcastName) =>
                DescriptionRegexTokens.Create(pattern, title, podcastName)?.Match(description) is
                    { Success: true } match
                    ? match.Groups["body"].Value
                    : description);
    }

    private string Term() => "term" + _fixture.CreateGuid().ToString("N")[..8];

    [Fact(DisplayName =
        "Subject matching: a subject term that appears only in the part of the description stripped by the " +
        "podcast's description regex (here the {podcast-name} prefix) does not match, because the regex is applied " +
        "with the episode title and podcast name.")]
    public async Task term_only_in_stripped_prefix_does_not_match()
    {
        // Arrange
        var prefixTerm = Term();
        var podcastName = prefixTerm + " " + Term();
        var episode = _fixture.BuildEpisode()
            .Customize(e => e.Description = podcastName + " " + _fixture.CreateTitle())
            .Create();
        _subjects = [new Subject(prefixTerm)];
        var sut = _mocker.CreateInstance<SubjectService>();

        // Act
        var matches = await sut.Match(episode, descriptionRegex: PodcastNamePrefixPattern, podcastName: podcastName);

        // Assert
        matches.Should().BeEmpty();
        _mocker.GetMock<ITextSanitiser>().Verify(x => x.ExtractDescription(
            episode.Description, PodcastNamePrefixPattern, episode.Title, podcastName), Times.Once);
    }

    [Fact(DisplayName =
        "Subject matching: a subject term in the kept body of the description matches from the description, and " +
        "the description regex is applied once per episode however many subjects are checked.")]
    public async Task term_in_kept_body_matches_and_regex_applied_once()
    {
        // Arrange
        var bodyTerm = Term();
        var podcastName = Term();
        var episode = _fixture.BuildEpisode()
            .Customize(e => e.Description = podcastName + " " + _fixture.CreateTitle() + " " + bodyTerm)
            .Create();
        _subjects = [new Subject(bodyTerm), new Subject(Term()), new Subject(Term())];
        var sut = _mocker.CreateInstance<SubjectService>();

        // Act
        var matches = (await sut.Match(episode, descriptionRegex: PodcastNamePrefixPattern,
            podcastName: podcastName)).ToArray();

        // Assert
        matches.Should().ContainSingle().Which.Subject.Name.Should().Be(bodyTerm);
        matches.Single().MatchResults.Should().ContainSingle()
            .Which.Source.Should().Be(SubjectMatchSource.Description);
        _mocker.GetMock<ITextSanitiser>().Verify(x => x.ExtractDescription(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>()), Times.Once);
    }
}

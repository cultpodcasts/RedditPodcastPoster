using AutoFixture;
using FluentAssertions;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Subjects;
using RedditPodcastPoster.Subjects.Enrichers;
using RedditPodcastPoster.Subjects.Matching;
using RedditPodcastPoster.Subjects.Models;

namespace RedditPodcastPoster.Subjects.Tests;

public class SubjectEnricherTests
{
    private readonly Fixture _fixture;
    private readonly AutoMocker _mocker;
    private IList<SubjectMatch> _subjectMatches;

    public SubjectEnricherTests()
    {
        _fixture = new Fixture();
        _mocker = new AutoMocker();
        _subjectMatches = _fixture.CreateMany<SubjectMatch>().ToList();
        _mocker.GetMock<ISubjectMatcher>()
            .Setup(x => x.MatchSubjects(It.IsAny<Episode>(), It.IsAny<SubjectEnrichmentOptions>()))
            .ReturnsAsync(() => _subjectMatches);
        _fixture.Customize<Episode>(x => x.Without(o => o.Subjects).Without(o => o.Matches));
    }

    [Fact(DisplayName =
        "When enrich finds no subject matches, it applies the podcast default subject.")]
    public async Task EnrichSubjects_WithNoMatches_AddsDefaultSubject()
    {
        // Arrange
        var episode = _fixture.Create<Episode>();
        var options = _fixture.Create<SubjectEnrichmentOptions>();
        _subjectMatches = new List<SubjectMatch>();

        var sut = _mocker.CreateInstance<SubjectEnricher>();

        // Act
        var result = await sut.EnrichSubjects(episode, options);

        // Assert
        result.Additions.Should().ContainSingle();
        result.Additions.Should().Contain(options.DefaultSubject);
        result.Removals.Should().HaveCount(0);
        episode.Matches.Should().ContainSingle(m =>
            m.Subject == options.DefaultSubject && m.Source == SubjectMatchSource.PodcastDefault);
    }

    [Fact(DisplayName =
        "When enrich finds only invisible subject matches, it also applies the podcast default and records PodcastDefault provenance.")]
    public async Task EnrichSubjects_WithOnlyInvisibleMatches_AddsDefaultSubject()
    {
        // Arrange
        var invisibleSubjectName = "_invisible";
        var defaultSubject = _fixture.Create<string>();
        var episode = _fixture.Create<Episode>();
        var options = _fixture.Build<SubjectEnrichmentOptions>()
            .With(x => x.DefaultSubject, defaultSubject)
            .Create();
        var invisibleSubject = new Subject(invisibleSubjectName);
        var subjectMatch = new SubjectMatch(
            invisibleSubject,
            [new MatchResult(invisibleSubjectName, 1)]
        );
        _subjectMatches = [subjectMatch];
        var sut = _mocker.CreateInstance<SubjectEnricher>();

        // Act
        var result = await sut.EnrichSubjects(episode, options);

        // Assert
        result.Additions.Should().HaveCount(2);
        result.Additions.Should().BeEquivalentTo([defaultSubject, invisibleSubjectName]);
        result.Removals.Should().HaveCount(0);
        episode.Subjects.Should().Contain(defaultSubject);
        episode.Subjects.Should().Contain(invisibleSubjectName);
        episode.Matches.Should().ContainSingle(m =>
            m.Subject == defaultSubject && m.Source == SubjectMatchSource.PodcastDefault);
    }

    [Fact(DisplayName =
        "When episode already has subjects covering matcher results (including invisible), enrich adds nothing.")]
    public async Task EnrichSubjects_WithExistingAndInvisibleMatchesEquallingPrematched_AddsDefaultSubject()
    {
        // Arrange
        var invisibleSubjectName = "_invisible";
        var existing = _fixture.Create<string>();
        var episode = _fixture.Build<Episode>().With(x => x.Subjects, [existing, invisibleSubjectName]).Create();
        var options = _fixture.Create<SubjectEnrichmentOptions>();
        var invisibleSubject = new Subject(invisibleSubjectName);
        var invisibleSubjectMatch = new SubjectMatch(
            invisibleSubject,
            [new MatchResult(invisibleSubjectName, 1)]
        );
        var existingSubjectMatch = new SubjectMatch(new Subject(existing), [new MatchResult(existing, 1)]);
        _subjectMatches = [invisibleSubjectMatch, existingSubjectMatch];
        var sut = _mocker.CreateInstance<SubjectEnricher>();

        // Act
        var result = await sut.EnrichSubjects(episode, options);

        // Assert
        result.Additions.Should().HaveCount(0);
        result.Removals.Should().HaveCount(0);
    }

    [Fact(DisplayName =
        "When enrich finds subject matches, those subject names are returned as additions.")]
    public async Task EnrichSubjects_WithMatches_AddsMatchedSubjects()
    {
        // Arrange
        var episode = _fixture.Create<Episode>();
        var options = _fixture.Create<SubjectEnrichmentOptions>();
        var sut = _mocker.CreateInstance<SubjectEnricher>();

        // Act
        var result = await sut.EnrichSubjects(episode, options);

        // Assert
        result.Additions.Should().BeEquivalentTo(_subjectMatches.Select(x => x.Subject.Name));
    }

    [Fact(DisplayName =
        "When a matched subject is also the podcast default, enrich lists the default subject first among additions.")]
    public async Task EnrichSubjects_WithMatchesAndDefaultSubject_AddsDefaultSubjectFirst()
    {
        // Arrange
        var episode = _fixture.Create<Episode>();
        var options = _fixture.Build<SubjectEnrichmentOptions>()
            .With(x => x.DefaultSubject, _subjectMatches.First().Subject.Name).Create();
        var sut = _mocker.CreateInstance<SubjectEnricher>();

        // Act
        var result = await sut.EnrichSubjects(episode, options);

        // Assert
        result.Additions.Should().StartWith(options.DefaultSubject);
    }

    [Fact(DisplayName =
        "When the user removed a subject, enrich does not re-add that subject from matcher results.")]
    public async Task EnrichSubjects_DoesNotReAddUserRemovedSubject()
    {
        // Arrange
        var removedSubject = _subjectMatches.First().Subject.Name;
        var episode = _fixture.Build<Episode>()
            .With(x => x.Subjects, [])
            .With(x => x.RemovedSubjects, [removedSubject])
            .Create();
        var options = _fixture.Create<SubjectEnrichmentOptions>();
        var sut = _mocker.CreateInstance<SubjectEnricher>();

        // Act
        var result = await sut.EnrichSubjects(episode, options);

        // Assert
        result.Additions.Should().NotContain(removedSubject);
        episode.Subjects.Should().NotContain(removedSubject);
    }

    [Fact(DisplayName =
        "When the user removed the podcast default subject, enrich does not apply that default.")]
    public async Task EnrichSubjects_DoesNotApplyDefaultSubjectWhenUserRemovedIt()
    {
        // Arrange
        var defaultSubject = _fixture.Create<string>();
        var episode = _fixture.Build<Episode>()
            .With(x => x.Subjects, [])
            .With(x => x.RemovedSubjects, [defaultSubject])
            .Create();
        var options = _fixture.Build<SubjectEnrichmentOptions>()
            .With(x => x.DefaultSubject, defaultSubject)
            .Create();
        _subjectMatches = [];
        var sut = _mocker.CreateInstance<SubjectEnricher>();

        // Act
        var result = await sut.EnrichSubjects(episode, options);

        // Assert
        result.Additions.Should().BeEmpty();
        episode.Subjects.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "When matcher returns title and description evidence, enrich populates episode matches from that evidence.")]
    public async Task EnrichSubjects_PopulatesMatchesForMatchedSubjects()
    {
        // Arrange
        var subjectName = _fixture.Create<string>();
        var term = _fixture.Create<string>();
        var episode = _fixture.Create<Episode>();
        var options = _fixture.Create<SubjectEnrichmentOptions>();
        _subjectMatches =
        [
            new SubjectMatch(
                new Subject(subjectName),
                [
                    new MatchResult(term, 1, SubjectMatchSource.Title),
                    new MatchResult(term, 1, SubjectMatchSource.Description)
                ])
        ];
        var sut = _mocker.CreateInstance<SubjectEnricher>();

        // Act
        await sut.EnrichSubjects(episode, options);

        // Assert
        episode.Matches.Should().HaveCount(2);
        episode.Matches.Should().Contain(m =>
            m.Subject == subjectName && m.Term == term && m.Source == SubjectMatchSource.Title);
        episode.Matches.Should().Contain(m =>
            m.Subject == subjectName && m.Term == term && m.Source == SubjectMatchSource.Description);
    }

    [Fact(DisplayName =
        "When enrich applies the podcast default subject with no title/description hits, matches records PodcastDefault provenance.")]
    public async Task EnrichSubjects_RecordsPodcastDefaultMatchForDefaultSubject()
    {
        // Arrange
        var defaultSubject = _fixture.Create<string>();
        var episode = _fixture.Create<Episode>();
        var options = _fixture.Build<SubjectEnrichmentOptions>()
            .With(x => x.DefaultSubject, defaultSubject)
            .Create();
        _subjectMatches = [];
        var sut = _mocker.CreateInstance<SubjectEnricher>();

        // Act
        await sut.EnrichSubjects(episode, options);

        // Assert
        episode.Subjects.Should().Contain(defaultSubject);
        episode.Matches.Should().ContainSingle(m =>
            m.Subject == defaultSubject &&
            m.Term == string.Empty &&
            m.Source == SubjectMatchSource.PodcastDefault);
    }

    [Fact(DisplayName =
        "When the user removed a subject, enrich does not populate match evidence for that subject.")]
    public async Task EnrichSubjects_DoesNotPopulateMatchesForUserRemovedSubject()
    {
        // Arrange
        var removedSubject = _fixture.Create<string>();
        var term = _fixture.Create<string>();
        var episode = _fixture.Build<Episode>()
            .With(x => x.Subjects, [])
            .With(x => x.RemovedSubjects, [removedSubject])
            .Create();
        var options = new SubjectEnrichmentOptions(null, null, null, string.Empty);
        var subjectMatches = new List<SubjectMatch>
        {
            new(
                new Subject(removedSubject),
                [new MatchResult(term, 1, SubjectMatchSource.Title)])
        };
        _mocker.GetMock<ISubjectMatcher>()
            .Setup(x => x.MatchSubjects(It.IsAny<Episode>(), It.IsAny<SubjectEnrichmentOptions?>()))
            .ReturnsAsync(subjectMatches);
        var sut = _mocker.CreateInstance<SubjectEnricher>();

        // Act
        await sut.EnrichSubjects(episode, options);

        // Assert
        episode.Matches.Should().NotContain(m =>
            m.Subject.Equals(removedSubject, StringComparison.OrdinalIgnoreCase));
        episode.Subjects.Should().NotContain(removedSubject);
    }
}

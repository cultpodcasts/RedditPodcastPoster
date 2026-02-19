using AutoFixture;
using FluentAssertions;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Models;
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
        _fixture.Customize<Episode>(x => x.Without(o => o.Subjects));
    }

    [Fact]
    public async Task EnrichSubjects_WithNoMatches_AddsDefaultSubject()
    {
        // arrange
        var episode = _fixture.Create<Episode>();
        var options = _fixture.Create<SubjectEnrichmentOptions>();
        _subjectMatches = new List<SubjectMatch>();

        var sut = _mocker.CreateInstance<SubjectEnricher>();

        // act
        var result = await sut.EnrichSubjects(episode, options);

        // assert
        result.Additions.Should().ContainSingle();
        result.Additions.Should().Contain(options.DefaultSubject);
        result.Removals.Should().HaveCount(0);
    }


    [Fact]
    public async Task EnrichSubjects_WithOnlyInvisibleMatches_AddsDefaultSubject()
    {
        // arrange
        var invisibleSubjectName = "_invisible";
        var episode = _fixture.Create<Episode>();
        var options = _fixture.Create<SubjectEnrichmentOptions>();
        var invisibleSubject = new Subject(invisibleSubjectName);
        var subjectMatch = new SubjectMatch(
            invisibleSubject,
            [new MatchResult(invisibleSubjectName, 1)]
        );
        _subjectMatches = [subjectMatch];
        var sut = _mocker.CreateInstance<SubjectEnricher>();
        // act
        var result = await sut.EnrichSubjects(episode, options);
        // assert
        result.Additions.Should().HaveCount(2);
        result.Additions.Should().BeEquivalentTo([options.DefaultSubject, invisibleSubjectName]);
        result.Removals.Should().HaveCount(0);
    }

    [Fact]
    public async Task EnrichSubjects_WithExistingAndInvisibleMatchesEquallingPrematched_AddsDefaultSubject()
    {
        // arrange
        var invisibleSubjectName = "_invisible";
        var existing = "existing";
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
        // act
        var result = await sut.EnrichSubjects(episode, options);
        // assert
        result.Additions.Should().HaveCount(0);
        result.Removals.Should().HaveCount(0);
    }

    [Fact]
    public async Task EnrichSubjects_WithMatches_AddsMatchedSubjects()
    {
        // arrange
        var episode = _fixture.Create<Episode>();
        var options = _fixture.Create<SubjectEnrichmentOptions>();
        var sut = _mocker.CreateInstance<SubjectEnricher>();
        // act
        var result = await sut.EnrichSubjects(episode, options);
        // assert
        result.Additions.Should().BeEquivalentTo(_subjectMatches.Select(x => x.Subject.Name));
    }

    [Fact]
    public async Task EnrichSubjects_WithMatchesAndDefaultSubject_AddsDefaultSubjectFirst()
    {
        // arrange
        var episode = _fixture.Create<Episode>();
        var options = _fixture.Build<SubjectEnrichmentOptions>()
            .With(x => x.DefaultSubject, _subjectMatches.First().Subject.Name).Create();
        var sut = _mocker.CreateInstance<SubjectEnricher>();
        // act
        var result = await sut.EnrichSubjects(episode, options);
        // assert
        result.Additions.Should().StartWith(options.DefaultSubject);
    }
}
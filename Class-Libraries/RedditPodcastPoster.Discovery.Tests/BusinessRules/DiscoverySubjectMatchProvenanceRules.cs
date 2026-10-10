using System.Text.Json;
using AutoFixture;
using FluentAssertions;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Discovery.Adapters;
using RedditPodcastPoster.Discovery.Models;
using RedditPodcastPoster.Discovery.Services;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.Discovery;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Subjects;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.Subjects.Models;
using RedditPodcastPoster.Subjects.Matching;

namespace RedditPodcastPoster.Discovery.Tests.BusinessRules;

public class DiscoverySubjectMatchProvenanceRules
{
    private readonly Fixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private IList<SubjectMatch> _subjectMatches = [];

    public DiscoverySubjectMatchProvenanceRules()
    {
        _mocker.GetMock<ISubjectMatcher>()
            .Setup(x => x.MatchSubjects(It.IsAny<Episode>(), It.IsAny<SubjectEnrichmentOptions>()))
            .ReturnsAsync(() => _subjectMatches);
    }

    private EnrichedEpisodeResult CreateEpisodeResult() =>
        new(new EpisodeResult(
                _fixture.Create<string>(), _fixture.Create<DateTime>(), _fixture.Create<string>(),
                _fixture.Create<string>(), null, _fixture.Create<string>(), _fixture.Create<string>(),
                DiscoverService.YouTube),
            []);

    [Fact(DisplayName =
        "When discovery matches a subject, the result records which term matched in which field, so curators can see why the subject was suggested.")]
    public async Task discovery_result_records_subject_match_provenance()
    {
        // Arrange
        var subject = new Subject(_fixture.Create<string>());
        var alias = _fixture.Create<string>();
        _subjectMatches = [new SubjectMatch(subject, [new MatchResult(alias, 1, SubjectMatchSource.Title)])];
        var sut = _mocker.CreateInstance<EnrichedEpisodeResultAdapter>();

        // Act
        var result = await sut.ToDiscoveryResult(CreateEpisodeResult());

        // Assert
        result.Subjects.Should().Equal(subject.Name);
        result.SubjectMatches.Should().ContainSingle(m =>
            m.Subject == subject.Name && m.Term == alias && m.Source == SubjectMatchSource.Title);
    }

    [Fact(DisplayName =
        "When a discovery result persisted before provenance existed is read, subjectMatches is empty rather than null, so old documents stay readable.")]
    public void legacy_discovery_result_deserializes_with_empty_subject_matches()
    {
        // Arrange
        var subject = _fixture.Create<string>();
        var json = $$"""{"id":"{{Guid.NewGuid()}}","subjects":["{{subject}}"]}""";

        // Act
        var result = JsonSerializer.Deserialize<DiscoveryResult>(json)!;

        // Assert
        result.Subjects.Should().Equal(subject);
        result.SubjectMatches.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "When a discovery result is serialized, subjectMatches is written with the source as a string, consistent with episode matches.")]
    public void discovery_result_serializes_subject_matches_with_string_source()
    {
        // Arrange
        var match = new PlayableSubjectMatch
        {
            Subject = _fixture.Create<string>(), Term = _fixture.Create<string>(),
            Source = SubjectMatchSource.Description
        };
        var result = new DiscoveryResult { SubjectMatches = [match] };

        // Act
        var json = JsonSerializer.Serialize(result);

        // Assert
        json.Should().Contain("\"subjectMatches\":[{\"subject\":");
        json.Should().Contain("\"source\":\"Description\"");
    }

    [Fact(DisplayName =
        "When duplicates are collapsed, the surviving result keeps its subject match provenance, because deduplication must not lose why subjects matched.")]
    public void deduplication_preserves_subject_match_provenance()
    {
        // Arrange
        var match = new PlayableSubjectMatch
        {
            Subject = _fixture.Create<string>(), Term = _fixture.Create<string>(),
            Source = SubjectMatchSource.Title
        };
        var result = new DiscoveryResult
        {
            EpisodeName = _fixture.Create<string>(),
            Subjects = [match.Subject],
            SubjectMatches = [match]
        };

        // Act
        var deduplicated = new DiscoveryResultDeduplicator().Deduplicate([result]);

        // Assert
        deduplicated.Should().ContainSingle().Which.SubjectMatches.Should().ContainSingle(m =>
            m.Subject == match.Subject && m.Term == match.Term && m.Source == SubjectMatchSource.Title);
    }
}

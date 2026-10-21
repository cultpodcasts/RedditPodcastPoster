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
        "When a discovery result persisted before provenance existed is read (key missing), subjectMatches is null, so historic results are distinguishable from results with no evidence.")]
    public void legacy_discovery_result_deserializes_with_empty_subject_matches()
    {
        // Arrange
        var subject = _fixture.Create<string>();
        var json = $$"""{"id":"{{_fixture.Create<Guid>()}}","subjects":["{{subject}}"]}""";

        // Act
        var result = JsonSerializer.Deserialize<DiscoveryResult>(json)!;

        // Assert
        result.Subjects.Should().Equal(subject);
        result.SubjectMatches.Should().BeNull();
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
        "When a stored discovery result has an explicit null subjectMatches, it reads as null, because null means provenance was not recorded.")]
    public void explicit_null_subject_matches_deserializes_as_empty()
    {
        // Arrange
        var json = $$"""{"id":"{{_fixture.Create<Guid>()}}","subjectMatches":null}""";

        // Act
        var result = JsonSerializer.Deserialize<DiscoveryResult>(json)!;

        // Assert
        result.SubjectMatches.Should().BeNull();
    }

    [Fact(DisplayName =
        "When a stored discovery result has an empty subjectMatches, it reads as an empty list, because provenance was recorded with no evidence.")]
    public void empty_subject_matches_deserializes_as_empty()
    {
        // Arrange
        var json = $$"""{"id":"{{_fixture.Create<Guid>()}}","subjectMatches":[]}""";

        // Act
        var result = JsonSerializer.Deserialize<DiscoveryResult>(json)!;

        // Assert
        result.SubjectMatches.Should().NotBeNull().And.BeEmpty();
    }

    [Fact(DisplayName =
        "When a populated subjectMatches is serialised and read back, every match survives unchanged, so stored provenance round-trips.")]
    public void populated_subject_matches_round_trip()
    {
        // Arrange
        var match = new PlayableSubjectMatch
        {
            Subject = _fixture.Create<string>(), Term = _fixture.Create<string>(),
            Source = SubjectMatchSource.Description
        };
        var original = new DiscoveryResult { SubjectMatches = [match] };

        // Act
        var result = JsonSerializer.Deserialize<DiscoveryResult>(JsonSerializer.Serialize(original))!;

        // Assert
        result.SubjectMatches.Should().BeEquivalentTo([match]);
    }

    [Fact(DisplayName =
        "When discovery matches no subjects, subjectMatches is an empty list, not null, so new results are distinguishable from historic ones.")]
    public async Task discovery_result_without_matches_records_empty_subject_matches()
    {
        // Arrange
        _subjectMatches = [];
        var sut = _mocker.CreateInstance<EnrichedEpisodeResultAdapter>();

        // Act
        var result = await sut.ToDiscoveryResult(CreateEpisodeResult());

        // Assert
        result.SubjectMatches.Should().NotBeNull().And.BeEmpty();
    }

    [Theory(DisplayName =
        "When a result is deduplicated, null subjectMatches stays null and an empty list stays empty, because dedup must not change whether provenance was recorded.")]
    [InlineData(true)]
    [InlineData(false)]
    public void deduplication_preserves_null_versus_empty_subject_matches(bool historic)
    {
        // Arrange
        var result = new DiscoveryResult
        {
            EpisodeName = _fixture.Create<string>(),
            SubjectMatches = historic ? null : []
        };

        // Act
        var survivor = new DiscoveryResultDeduplicator().Deduplicate([result]).Single();

        // Assert
        if (historic)
        {
            survivor.SubjectMatches.Should().BeNull();
        }
        else
        {
            survivor.SubjectMatches.Should().NotBeNull().And.BeEmpty();
        }
    }

    [Fact(DisplayName =
        "When discovery matches several subjects, subjectMatches is grouped in the same order as subjects, so the two lists never disagree.")]
    public async Task subject_matches_follow_subjects_order()
    {
        // Arrange
        var weaker = new Subject(_fixture.Create<string>());
        var stronger = new Subject(_fixture.Create<string>());
        _subjectMatches =
        [
            new SubjectMatch(weaker, [new MatchResult(_fixture.Create<string>(), 1, SubjectMatchSource.Description)]),
            new SubjectMatch(stronger,
            [
                new MatchResult(_fixture.Create<string>(), 2, SubjectMatchSource.Title),
                new MatchResult(_fixture.Create<string>(), 3, SubjectMatchSource.Description)
            ])
        ];
        var sut = _mocker.CreateInstance<EnrichedEpisodeResultAdapter>();

        // Act
        var result = await sut.ToDiscoveryResult(CreateEpisodeResult());

        // Assert
        result.Subjects.Should().Equal(stronger.Name, weaker.Name);
        result.SubjectMatches!.Select(m => m.Subject).Distinct().Should().Equal(result.Subjects);
    }

    [Fact(DisplayName =
        "When two duplicate discovery results are collapsed, the survivor's subjects and subject matches come from the same result, so provenance agrees with the subjects shown.")]
    public void deduplication_keeps_subjects_and_subject_matches_in_agreement()
    {
        // Arrange
        var spotifyUrl = new Uri($"https://open.spotify.com/episode/{_fixture.Create<Guid>():N}");
        var episodeName = _fixture.Create<string>();
        var showName = _fixture.Create<string>();
        var released = _fixture.Create<DateTime>();
        DiscoveryResult CreateDuplicate()
        {
            var match = new PlayableSubjectMatch
            {
                Subject = _fixture.Create<string>(), Term = _fixture.Create<string>(),
                Source = SubjectMatchSource.Title
            };
            return new DiscoveryResult
            {
                EpisodeName = episodeName,
                ShowName = showName,
                Released = released,
                Urls = { Spotify = spotifyUrl },
                Subjects = [match.Subject],
                SubjectMatches = [match]
            };
        }

        var first = CreateDuplicate();
        var second = CreateDuplicate();

        // Act
        var deduplicated = new DiscoveryResultDeduplicator().Deduplicate([first, second]);

        // Assert
        var survivor = deduplicated.Should().ContainSingle().Subject;
        survivor.SubjectMatches!.Select(m => m.Subject).Distinct().Should().Equal(survivor.Subjects);
        survivor.Subjects.Should().ContainSingle().Which.Should().BeOneOf(first.Subjects.Single(), second.Subjects.Single());
        survivor.SubjectMatches.Should().NotContain(m => first.SubjectMatches!.Contains(m) || second.SubjectMatches!.Contains(m),
            "the survivor owns copies, not the inputs' instances");
    }
}

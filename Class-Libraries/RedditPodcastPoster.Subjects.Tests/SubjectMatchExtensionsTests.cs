using AutoFixture;
using FluentAssertions;
using RedditPodcastPoster.Models.Subjects;
using RedditPodcastPoster.Subjects.Extensions;
using RedditPodcastPoster.Subjects.Models;

namespace RedditPodcastPoster.Subjects.Tests;

public class SubjectMatchExtensionsTests
{
    private readonly Fixture _fixture = new();

    [Fact(DisplayName =
        "When a subject match has title and description evidence, each becomes provenance carrying the subject, the matched term and its field, so curators can see why it matched.")]
    public void to_playable_subject_matches_projects_each_sourced_result()
    {
        // Arrange
        var subject = new Subject(_fixture.Create<string>());
        var titleTerm = _fixture.Create<string>();
        var descriptionTerm = _fixture.Create<string>();
        var match = new SubjectMatch(subject,
        [
            new MatchResult(titleTerm, 1, SubjectMatchSource.Title),
            new MatchResult(descriptionTerm, 2, SubjectMatchSource.Description)
        ]);

        // Act
        var result = match.ToPlayableSubjectMatches().ToArray();

        // Assert
        result.Should().HaveCount(2);
        result.Should().ContainSingle(m =>
            m.Subject == subject.Name && m.Term == titleTerm && m.Source == SubjectMatchSource.Title);
        result.Should().ContainSingle(m =>
            m.Subject == subject.Name && m.Term == descriptionTerm && m.Source == SubjectMatchSource.Description);
    }

    [Fact(DisplayName =
        "When a match result has no source, it is skipped, because it carries no title/description evidence.")]
    public void to_playable_subject_matches_skips_unsourced_results()
    {
        // Arrange
        var match = new SubjectMatch(new Subject(_fixture.Create<string>()),
            [new MatchResult(_fixture.Create<string>(), 1)]);

        // Act
        var result = match.ToPlayableSubjectMatches();

        // Assert
        result.Should().BeEmpty();
    }
}

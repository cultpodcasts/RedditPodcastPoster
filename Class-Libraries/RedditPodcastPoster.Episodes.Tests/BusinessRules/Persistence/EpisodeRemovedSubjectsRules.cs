using FluentAssertions;
using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Episodes.Tests.BusinessRules.Persistence;

public class EpisodeRemovedSubjectsRules
{
    [Fact(DisplayName = "Removing a subject adds it to removedSubjects.")]
    public void apply_user_subjects_removal_adds_to_removed_subjects()
    {
        var episode = new Episode
        {
            Subjects = ["Cults", "Scientology"]
        };

        var changed = episode.ApplyUserSubjects(["Scientology"]);

        changed.Should().BeTrue();
        episode.Subjects.Should().Equal("Scientology");
        episode.RemovedSubjects.Should().Equal("Cults");
    }

    [Fact(DisplayName = "Re-adding a removed subject clears it from removedSubjects.")]
    public void apply_user_subjects_readd_clears_removed_subjects()
    {
        var episode = new Episode
        {
            Subjects = ["Scientology"],
            RemovedSubjects = ["Cults"]
        };

        var changed = episode.ApplyUserSubjects(["Scientology", "Cults"]);

        changed.Should().BeTrue();
        episode.Subjects.Should().Equal("Scientology", "Cults");
        episode.RemovedSubjects.Should().BeEmpty();
    }

    [Fact(DisplayName = "Identical subject lists are a no-op.")]
    public void apply_user_subjects_no_change_returns_false()
    {
        var episode = new Episode
        {
            Subjects = ["Cults"],
            RemovedSubjects = ["Scientology"]
        };

        var changed = episode.ApplyUserSubjects(["Cults"]);

        changed.Should().BeFalse();
        episode.RemovedSubjects.Should().Equal("Scientology");
    }

    [Fact(DisplayName = "Removed-subject tracking is case-insensitive.")]
    public void apply_user_subjects_is_case_insensitive()
    {
        var episode = new Episode
        {
            Subjects = ["Scientology"],
            RemovedSubjects = ["cults"]
        };

        episode.ApplyUserSubjects(["Scientology", "CULTS"]);

        episode.RemovedSubjects.Should().BeEmpty();
        episode.Subjects.Should().Equal("Scientology", "CULTS");
    }

    [Fact(DisplayName = "Removing a subject clears its match records.")]
    public void apply_user_subjects_removal_clears_matches()
    {
        var episode = new Episode
        {
            Subjects = ["Cults", "Scientology"],
            Matches =
            [
                new EpisodeSubjectMatch
                {
                    Subject = "Cults",
                    Term = "cult",
                    Source = SubjectMatchSource.Title
                },
                new EpisodeSubjectMatch
                {
                    Subject = "Scientology",
                    Term = "scientology",
                    Source = SubjectMatchSource.Description
                }
            ]
        };

        episode.ApplyUserSubjects(["Scientology"]);

        episode.Matches.Should().ContainSingle();
        episode.Matches[0].Subject.Should().Be("Scientology");
    }
}

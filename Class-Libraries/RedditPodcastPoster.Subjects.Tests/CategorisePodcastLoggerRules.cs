using AutoFixture;
using FluentAssertions;
using RedditPodcastPoster.Models.Subjects;
using RedditPodcastPoster.Subjects.Categorisation;

namespace RedditPodcastPoster.Subjects.Tests;

public class CategorisePodcastLoggerRules
{
    private readonly Fixture _fixture = new();

    [Fact(DisplayName = "FormatMessage uses stable Categorise podcast: prefix with ids and subject delta.")]
    public void format_message_includes_podcast_episodes_and_delta()
    {
        // Arrange
        var podcastId = _fixture.Create<Guid>();
        var podcastName = _fixture.Create<string>();
        var episodeId = _fixture.Create<Guid>();
        var episodeTitle = _fixture.Create<string>();
        var subjects = _fixture.CreateMany<string>(2).ToArray();

        var deltas = new[]
        {
            CategoriseEpisodeDelta.From(
                episodeId,
                episodeTitle,
                before: [],
                after: subjects,
                persisted: true)
        };

        // Act
        var message = CategorisePodcastLogger.FormatMessage(podcastId, podcastName, deltas);

        // Assert
        message.Should().StartWith(CategorisePodcastLogger.MessagePrefix);
        message.Should().Contain($"podcast-id='{podcastId}'");
        message.Should().Contain($"podcast-name='{podcastName}'");
        message.Should().Contain($"episode-id='{episodeId}'");
        message.Should().Contain($"title='{episodeTitle}'");
        message.Should().Contain($"before→after=[]→['{subjects[0]}', '{subjects[1]}']");
        message.Should().Contain($"added=['{subjects[0]}', '{subjects[1]}']");
        message.Should().Contain("removed=[]");
        message.Should().Contain("persisted=True");
    }

    [Fact(DisplayName = "FormatMessage reports unchanged when subjects did not change.")]
    public void format_message_reports_unchanged()
    {
        // Arrange
        var episodeId = _fixture.Create<Guid>();
        var deltas = new[]
        {
            CategoriseEpisodeDelta.From(
                episodeId,
                _fixture.Create<string>(),
                before: [],
                after: [],
                persisted: false)
        };

        // Act
        var message = CategorisePodcastLogger.FormatMessage(_fixture.Create<Guid>(), _fixture.Create<string>(), deltas);

        // Assert
        message.Should().Contain($"episode-id='{episodeId}'");
        message.Should().Contain("unchanged before→after=[]");
        message.Should().Contain("persisted=False");
        message.Should().NotContain("added=");
    }

    [Fact(DisplayName = "CategoriseEpisodeDelta computes added and removed subjects.")]
    public void episode_delta_computes_added_and_removed()
    {
        // Arrange
        var keep = _fixture.Create<string>();
        var oldSubject = _fixture.Create<string>();
        var newSubject = _fixture.Create<string>();

        // Act
        var delta = CategoriseEpisodeDelta.From(
            _fixture.Create<Guid>(),
            _fixture.Create<string>(),
            before: [keep, oldSubject],
            after: [keep, newSubject],
            persisted: true);

        // Assert
        delta.Added.Should().BeEquivalentTo([newSubject]);
        delta.Removed.Should().BeEquivalentTo([oldSubject]);
        delta.Before.Should().BeEquivalentTo([keep, oldSubject]);
        delta.After.Should().BeEquivalentTo([keep, newSubject]);
    }

    [Fact(DisplayName = "FormatMessage lists multiple episodes for one podcast line.")]
    public void format_message_lists_multiple_episodes()
    {
        // Arrange
        var e1 = _fixture.Create<Guid>();
        var e2 = _fixture.Create<Guid>();
        var t1 = _fixture.Create<string>();
        var t2 = _fixture.Create<string>();
        var deltas = new[]
        {
            CategoriseEpisodeDelta.From(e1, t1, [], [_fixture.Create<string>()], true),
            CategoriseEpisodeDelta.From(e2, t2, [], [], false)
        };

        // Act
        var message = CategorisePodcastLogger.FormatMessage(_fixture.Create<Guid>(), _fixture.Create<string>(), deltas);

        // Assert
        message.Should().Contain($"episode-id='{e1}'");
        message.Should().Contain($"episode-id='{e2}'");
        message.Should().Contain($"title='{t1}'");
        message.Should().Contain($"title='{t2}'");
        message.Should().Contain(";");
    }
}

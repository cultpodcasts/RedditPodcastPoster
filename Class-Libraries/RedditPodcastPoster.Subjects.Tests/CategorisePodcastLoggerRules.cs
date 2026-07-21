using FluentAssertions;
using RedditPodcastPoster.Subjects.Categorisation;

namespace RedditPodcastPoster.Subjects.Tests;

public class CategorisePodcastLoggerRules
{
    [Fact(DisplayName = "FormatMessage uses stable Categorise podcast: prefix with ids and subject delta.")]
    public void format_message_includes_podcast_episodes_and_delta()
    {
        var podcastId = Guid.Parse("11111111-2222-3333-4444-555555555555");
        var episodeId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        var deltas = new[]
        {
            CategoriseEpisodeDelta.From(
                episodeId,
                "Preacher Boys Episode",
                before: [],
                after: ["Abuse", "Cult"],
                persisted: true)
        };

        var message = CategorisePodcastLogger.FormatMessage(podcastId, "Preacher Boys Podcast", deltas);

        message.Should().StartWith(CategorisePodcastLogger.MessagePrefix);
        message.Should().Contain($"podcast-id='{podcastId}'");
        message.Should().Contain("podcast-name='Preacher Boys Podcast'");
        message.Should().Contain($"episode-id='{episodeId}'");
        message.Should().Contain("title='Preacher Boys Episode'");
        message.Should().Contain("before→after=[]→['Abuse', 'Cult']");
        message.Should().Contain("added=['Abuse', 'Cult']");
        message.Should().Contain("removed=[]");
        message.Should().Contain("persisted=True");
    }

    [Fact(DisplayName = "FormatMessage reports unchanged when subjects did not change.")]
    public void format_message_reports_unchanged()
    {
        var episodeId = Guid.NewGuid();
        var deltas = new[]
        {
            CategoriseEpisodeDelta.From(
                episodeId,
                "Empty Title",
                before: [],
                after: [],
                persisted: false)
        };

        var message = CategorisePodcastLogger.FormatMessage(Guid.NewGuid(), "Some Podcast", deltas);

        message.Should().Contain($"episode-id='{episodeId}'");
        message.Should().Contain("unchanged before→after=[]");
        message.Should().Contain("persisted=False");
        message.Should().NotContain("added=");
    }

    [Fact(DisplayName = "CategoriseEpisodeDelta computes added and removed subjects.")]
    public void episode_delta_computes_added_and_removed()
    {
        var delta = CategoriseEpisodeDelta.From(
            Guid.NewGuid(),
            "t",
            before: ["Keep", "Old"],
            after: ["Keep", "New"],
            persisted: true);

        delta.Added.Should().BeEquivalentTo(["New"]);
        delta.Removed.Should().BeEquivalentTo(["Old"]);
        delta.Before.Should().BeEquivalentTo(["Keep", "Old"]);
        delta.After.Should().BeEquivalentTo(["Keep", "New"]);
    }

    [Fact(DisplayName = "FormatMessage lists multiple episodes for one podcast line.")]
    public void format_message_lists_multiple_episodes()
    {
        var e1 = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        var e2 = Guid.Parse("ffffffff-0000-1111-2222-333333333333");
        var deltas = new[]
        {
            CategoriseEpisodeDelta.From(e1, "One", [], ["A"], true),
            CategoriseEpisodeDelta.From(e2, "Two", [], [], false)
        };

        var message = CategorisePodcastLogger.FormatMessage(Guid.NewGuid(), "Pod", deltas);

        message.Should().Contain($"episode-id='{e1}'");
        message.Should().Contain($"episode-id='{e2}'");
        message.Should().Contain("title='One'");
        message.Should().Contain("title='Two'");
        message.Should().Contain(";");
    }
}

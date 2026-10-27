using FluentAssertions;
using EpisodeServiceBackfill;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using Xunit;

namespace EpisodeServiceBackfill.Tests;

public class EpisodeYouTubeIdRepairTests
{
    private readonly DomainTestFixture _fixture = new();

    [Fact(DisplayName =
        "A document with services.youtube.url and an ids object without youtube is planned as a patch of /ids/youtube only.")]
    public void plans_ids_youtube_patch_when_ids_object_exists()
    {
        // Arrange
        var (episodeId, podcastId, videoId) = Identity();
        var spotifyId = _fixture.CreateSpotifyId();
        var json = $$"""
            {"id":"{{episodeId}}","podcastId":"{{podcastId}}","_ts":1790000000,
             "ids":{"spotify":"{{spotifyId}}"},
             "services":{"youtube":{"url":"{{_fixture.DefaultYouTubeUrl(videoId)}}"} } }
            """;

        // Act
        var plan = EpisodeYouTubeIdRepair.Plan(json, out var reason);

        // Assert
        reason.Should().Be(EpisodeYouTubeIdRepair.SkipReason.None);
        plan.Should().NotBeNull();
        plan!.EpisodeId.Should().Be(episodeId);
        plan.PodcastId.Should().Be(podcastId);
        plan.YouTubeId.Should().Be(videoId);
        plan.PatchPath.Should().Be("/ids/youtube");
        EpisodeYouTubeIdRepair.PatchValue(plan).Should().Be(videoId);
    }

    [Theory(DisplayName =
        "A null or empty stored ids.youtube is treated as missing, as the 24 Sep migration left some documents with an empty string.")]
    [InlineData("null")]
    [InlineData("\"\"")]
    public void plans_patch_when_stored_id_is_null_or_empty(string stored)
    {
        // Arrange
        var (episodeId, podcastId, videoId) = Identity();
        var json = $$"""
            {"id":"{{episodeId}}","podcastId":"{{podcastId}}","ids":{"youtube":{{stored}}},
             "services":{"youtube":{"url":"https://youtu.be/{{videoId}}"} } }
            """;

        // Act
        var plan = EpisodeYouTubeIdRepair.Plan(json, out _);

        // Assert
        plan!.YouTubeId.Should().Be(videoId);
        plan.PatchPath.Should().Be("/ids/youtube");
    }

    [Fact(DisplayName =
        "A document with no ids object is planned as a patch that adds /ids with only youtube.")]
    public void plans_ids_object_when_absent()
    {
        // Arrange
        var (episodeId, podcastId, videoId) = Identity();
        var json = $$"""
            {"id":"{{episodeId}}","podcastId":"{{podcastId}}","removed":true,
             "services":{"youtube":{"url":"{{_fixture.DefaultYouTubeUrl(videoId)}}"} } }
            """;

        // Act
        var plan = EpisodeYouTubeIdRepair.Plan(json, out _);

        // Assert
        plan!.PatchPath.Should().Be("/ids");
        plan.Removed.Should().BeTrue();
        EpisodeYouTubeIdRepair.PatchValue(plan).Should()
            .BeEquivalentTo(new Dictionary<string, string> { ["youtube"] = videoId });
    }

    [Fact(DisplayName =
        "A document whose ids.youtube is already set is skipped, so re-running the repair writes nothing.")]
    public void skips_when_id_already_set()
    {
        // Arrange
        var (episodeId, podcastId, videoId) = Identity();
        var json = $$"""
            {"id":"{{episodeId}}","podcastId":"{{podcastId}}","ids":{"youtube":"{{videoId}}"},
             "services":{"youtube":{"url":"{{_fixture.DefaultYouTubeUrl(videoId)}}"} } }
            """;

        // Act
        var plan = EpisodeYouTubeIdRepair.Plan(json, out var reason);

        // Assert
        plan.Should().BeNull();
        reason.Should().Be(EpisodeYouTubeIdRepair.SkipReason.IdAlreadySet);
    }

    [Fact(DisplayName = "A YouTube playlist url has no video id and is skipped.")]
    public void skips_url_without_video_id()
    {
        // Arrange
        var (episodeId, podcastId, _) = Identity();
        var json = $$"""
            {"id":"{{episodeId}}","podcastId":"{{podcastId}}",
             "services":{"youtube":{"url":"https://www.youtube.com/playlist?list=PLsynthetic0000000000"} } }
            """;

        // Act
        var plan = EpisodeYouTubeIdRepair.Plan(json, out var reason);

        // Assert
        plan.Should().BeNull();
        reason.Should().Be(EpisodeYouTubeIdRepair.SkipReason.UrlHasNoVideoId);
    }

    [Fact(DisplayName = "A document with no YouTube service is skipped.")]
    public void skips_without_youtube_url()
    {
        // Arrange
        var (episodeId, podcastId, _) = Identity();
        var json = $$"""
            {"id":"{{episodeId}}","podcastId":"{{podcastId}}","services":{"spotify":{"url":"{{_fixture.DefaultSpotifyUrl(_fixture.CreateSpotifyId())}}"} } }
            """;

        // Act
        var plan = EpisodeYouTubeIdRepair.Plan(json, out var reason);

        // Assert
        plan.Should().BeNull();
        reason.Should().Be(EpisodeYouTubeIdRepair.SkipReason.NoYouTubeUrl);
    }

    [Fact(DisplayName =
        "The patch is guarded by a filter predicate on a missing ids.youtube, so it never overwrites an id set after the scan.")]
    public void patch_filter_requires_missing_id()
    {
        // Assert
        EpisodeYouTubeIdRepair.PatchFilterPredicate.Should().Contain("NOT IS_DEFINED(c.ids.youtube)");
        EpisodeYouTubeIdRepair.PatchFilterPredicate.Should().Contain("c.ids.youtube = ''");
        EpisodeYouTubeIdRepair.CandidateQuery.Should().Contain("IS_DEFINED(c.services.youtube.url)");
    }

    private (Guid EpisodeId, Guid PodcastId, string VideoId) Identity() =>
        (_fixture.CreateGuid(), _fixture.CreateGuid(), _fixture.CreateYouTubeId());
}

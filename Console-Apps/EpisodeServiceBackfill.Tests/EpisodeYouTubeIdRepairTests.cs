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
        "When the ids object exists, the patch applies only while ids.youtube is still missing and the url is still the one scanned, so a later id or a changed url is never overwritten.")]
    public void patch_filter_for_ids_youtube_path_requires_missing_id_and_same_url()
    {
        // Arrange
        var (episodeId, podcastId, videoId) = Identity();
        var url = _fixture.DefaultYouTubeUrl(videoId).ToString();
        var json = $$"""
            {"id":"{{episodeId}}","podcastId":"{{podcastId}}","ids":{"spotify":"{{_fixture.CreateSpotifyId()}}"},
             "services":{"youtube":{"url":"{{url}}"} } }
            """;
        var plan = EpisodeYouTubeIdRepair.Plan(json, out _)!;

        // Act
        var predicate = EpisodeYouTubeIdRepair.PatchFilterPredicateFor(plan);

        // Assert
        predicate.Should().Be(
            "FROM c WHERE (NOT IS_DEFINED(c.ids.youtube) OR IS_NULL(c.ids.youtube) OR c.ids.youtube = '') " +
            $"AND c.services.youtube.url = '{url}'");
    }

    [Fact(DisplayName =
        "When the document had no ids object, the /ids patch applies only while there is still no ids object and the url is unchanged, so Spotify or Apple ids written after the scan are never replaced.")]
    public void patch_filter_for_ids_path_requires_no_ids_object_and_same_url()
    {
        // Arrange
        var (episodeId, podcastId, videoId) = Identity();
        var url = $"https://youtu.be/{videoId}";
        var json = $$"""
            {"id":"{{episodeId}}","podcastId":"{{podcastId}}","services":{"youtube":{"url":"{{url}}"} } }
            """;
        var plan = EpisodeYouTubeIdRepair.Plan(json, out _)!;

        // Act
        var predicate = EpisodeYouTubeIdRepair.PatchFilterPredicateFor(plan);

        // Assert
        plan.PatchPath.Should().Be("/ids");
        predicate.Should().Be(
            $"FROM c WHERE (NOT IS_DEFINED(c.ids) OR IS_NULL(c.ids)) AND c.services.youtube.url = '{url}'");
    }

    [Fact(DisplayName =
        "The url pinned in the patch condition is the raw stored string, not a normalised Uri, because Cosmos compares it with the stored value byte for byte.")]
    public void plan_keeps_raw_stored_url()
    {
        // Arrange
        var (episodeId, podcastId, videoId) = Identity();
        var rawUrl = $"HTTPS://WWW.YouTube.com/watch?v={videoId}&t=1";
        var json = $$"""
            {"id":"{{episodeId}}","podcastId":"{{podcastId}}","ids":{},"services":{"youtube":{"url":"{{rawUrl}}"} } }
            """;

        // Act
        var plan = EpisodeYouTubeIdRepair.Plan(json, out _);

        // Assert
        plan!.Url.Should().Be(rawUrl);
        EpisodeYouTubeIdRepair.PatchFilterPredicateFor(plan).Should().EndWith($"c.services.youtube.url = '{rawUrl}'");
    }

    [Fact(DisplayName =
        "A quote in the stored url is escaped in the condition, so the url cannot break out of the SQL string literal.")]
    public void patch_filter_escapes_quote_in_url()
    {
        // Arrange
        var (episodeId, podcastId, videoId) = Identity();
        var json = $$"""
            {"id":"{{episodeId}}","podcastId":"{{podcastId}}","ids":{},
             "services":{"youtube":{"url":"https://www.youtube.com/watch?v={{videoId}}&title=it's"} } }
            """;
        var plan = EpisodeYouTubeIdRepair.Plan(json, out _)!;

        // Act
        var predicate = EpisodeYouTubeIdRepair.PatchFilterPredicateFor(plan);

        // Assert
        predicate.Should().EndWith(
            $"c.services.youtube.url = 'https://www.youtube.com/watch?v={videoId}&title=it\\'s'");
    }

    [Theory(DisplayName =
        "Cosmos SQL string literals escape backslashes, quotes and control characters, so any stored url round-trips into the condition unchanged.")]
    [InlineData(@"a\b", @"'a\\b'")]
    [InlineData("a'b", @"'a\'b'")]
    [InlineData("a\nb", @"'a\u000ab'")]
    [InlineData("plain", "'plain'")]
    public void sql_string_literal_escapes(string value, string expected)
    {
        // Arrange
        var stored = value;

        // Act
        var literal = EpisodeYouTubeIdRepair.SqlStringLiteral(stored);

        // Assert
        literal.Should().Be(expected);
    }

    [Fact(DisplayName =
        "A stored ids.youtube for a different video than the url is reported as a mismatch (and never planned as a patch), because podcast episodes keep their stored id.")]
    public void reports_stored_id_that_differs_from_url()
    {
        // Arrange
        var (episodeId, podcastId, urlVideoId) = Identity();
        var storedId = _fixture.CreateYouTubeId();
        var url = _fixture.DefaultYouTubeUrl(urlVideoId).ToString();
        var json = $$"""
            {"id":"{{episodeId}}","podcastId":"{{podcastId}}","ids":{"youtube":"{{storedId}}"},
             "services":{"youtube":{"url":"{{url}}"} } }
            """;

        // Act
        var mismatch = EpisodeYouTubeIdRepair.FindMismatch(json);
        var plan = EpisodeYouTubeIdRepair.Plan(json, out var reason);

        // Assert
        mismatch.Should().Be(new EpisodeYouTubeIdMismatch(episodeId, podcastId, storedId, urlVideoId, url));
        plan.Should().BeNull();
        reason.Should().Be(EpisodeYouTubeIdRepair.SkipReason.IdAlreadySet);
    }

    [Fact(DisplayName = "A stored ids.youtube that matches the url's video is not a mismatch.")]
    public void matching_stored_id_is_not_a_mismatch()
    {
        // Arrange
        var (episodeId, podcastId, videoId) = Identity();
        var json = $$"""
            {"id":"{{episodeId}}","podcastId":"{{podcastId}}","ids":{"youtube":"{{videoId}}"},
             "services":{"youtube":{"url":"https://youtu.be/{{videoId}}"} } }
            """;

        // Act
        var mismatch = EpisodeYouTubeIdRepair.FindMismatch(json);

        // Assert
        mismatch.Should().BeNull();
    }

    private (Guid EpisodeId, Guid PodcastId, string VideoId) Identity() =>
        (_fixture.CreateGuid(), _fixture.CreateGuid(), _fixture.CreateYouTubeId());
}

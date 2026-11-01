using System.Globalization;
using FluentAssertions;
using Google.Apis.YouTube.v3.Data;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq.AutoMock;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Episodes.TestSupport.TimeZones;
using RedditPodcastPoster.Models.Extensions;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.PodcastServices.YouTube.ChannelSnippets;
using RedditPodcastPoster.PodcastServices.YouTube.Clients;
using RedditPodcastPoster.PodcastServices.YouTube.Models;
using RedditPodcastPoster.PodcastServices.YouTube.Services;
using RedditPodcastPoster.PodcastServices.YouTube.Tests.Paging;
using Xunit;

namespace RedditPodcastPoster.PodcastServices.YouTube.Tests.BusinessRules.ReleaseTimes;

/// <summary>
/// YouTube indexing reads every release time and <see cref="IndexingContext.ReleasedSince"/> as UTC, and an
/// unspecified-kind value as UTC too, never as host-local time (#1194, same bug as #1191). These facts only catch a
/// regression when the host is not on UTC, so <see cref="YouTubeReleaseTimeEastOfUtcHostGuard"/> re-runs this class
/// under TZ=Pacific/Auckland on every build.
/// </summary>
public class YouTubeReleaseTimeUtcRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();

    public YouTubeReleaseTimeUtcRules()
    {
        _mocker.Use<ILogger<YouTubeSearcher>>(NullLogger<YouTubeSearcher>.Instance);
        _mocker.Use<ILogger<YouTubeChannelVideoSnippetsService>>(NullLogger<YouTubeChannelVideoSnippetsService>.Instance);
    }

    private FakeYouTubePagingApi UseFakeApi(FakeYouTubePagingApi api)
    {
        _mocker.GetMock<IYouTubeServiceWrapper>().SetupGet(x => x.YouTubeService).Returns(api.CreateService());
        return api;
    }

    /// <summary>A UTC instant with a time of day, as an unspecified-kind value (how a zone-less read arrives).</summary>
    private DateTime CreateUnspecifiedUtcInstant() =>
        DateTime.SpecifyKind(
            DomainTestFixture.UtcAtTime(-(_fixture.Create<int>() % 300 + 2), _fixture.CreateNonMidnightTimeOfDay()),
            DateTimeKind.Unspecified);

    private static DateTimeOffset AsUtc(DateTime unspecified) =>
        new(DateTime.SpecifyKind(unspecified, DateTimeKind.Utc), TimeSpan.Zero);

    private static DateTimeOffset ParsePublishedAfter(string? text)
    {
        text.Should().NotBeNull("the request should send publishedAfter");
        return DateTimeOffset.Parse(text!, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
    }

    [Fact(DisplayName =
        "When the indexing window starts at an unspecified-kind time, then a release one minute before it is outside the window and one at it is inside, because an unspecified window start is UTC and reading it as host-local time would move the window by the host's offset.")]
    public void released_since_date_reads_unspecified_window_as_utc()
    {
        // Arrange
        var since = CreateUnspecifiedUtcInstant();
        DateTimeOffset? atStart = AsUtc(since);
        DateTimeOffset? justBefore = AsUtc(since).AddMinutes(-1);

        // Act
        var atStartIncluded = atStart.ReleasedSinceDate(since);
        var justBeforeIncluded = justBefore.ReleasedSinceDate(since);

        // Assert
        atStartIncluded.Should().BeTrue();
        justBeforeIncluded.Should().BeFalse();
    }

    [Fact(DisplayName =
        "When a channel's latest videos are read with an unspecified-kind ReleasedSince, then search.list is asked for videos published after that same UTC instant, because the window start is UTC and must not move by the host's offset.")]
    public async Task channel_snippets_publishedAfter_is_the_utc_instant()
    {
        // Arrange
        var since = CreateUnspecifiedUtcInstant();
        var api = UseFakeApi(new FakeYouTubePagingApi()
            .Page("", new FakeYouTubePage(FakeYouTubePagingApi.Ids("s", 0, 1), null) { Newest = AsUtc(since).AddHours(1) }));
        var sut = _mocker.CreateInstance<YouTubeChannelVideoSnippetsService>();

        // Act
        await sut.GetLatestChannelVideoSnippets(_mocker.GetMock<IYouTubeServiceWrapper>().Object,
            new YouTubeChannelId("UC" + _fixture.Create<string>()), new IndexingContext(since));

        // Assert
        api.PublishedAfter.Should().NotBeEmpty();
        api.PublishedAfter.Select(ParsePublishedAfter).Should().AllSatisfy(x => x.Should().Be(AsUtc(since)));
    }

    [Fact(DisplayName =
        "When YouTube is searched with an unspecified-kind ReleasedSince, then search.list is asked for videos published after that UTC instant and a video published a minute before it is dropped, because the search window start is UTC, not host-local time.")]
    public async Task search_window_is_the_utc_instant()
    {
        // Arrange
        var since = CreateUnspecifiedUtcInstant();
        var ids = FakeYouTubePagingApi.Ids("v", 0, 2);
        var api = UseFakeApi(new FakeYouTubePagingApi()
            .Page("", new FakeYouTubePage(ids, null)
            {
                Dates = [AsUtc(since).AddMinutes(1), AsUtc(since).AddMinutes(-1)]
            }));
        var sut = _mocker.CreateInstance<YouTubeSearcher>();

        // Act
        var results = await sut.Search(_fixture.Create<string>(), new IndexingContext(since));

        // Assert
        api.PublishedAfter.Should().NotBeEmpty();
        api.PublishedAfter.Select(ParsePublishedAfter).Should().AllSatisfy(x => x.Should().Be(AsUtc(since)));
        results.Select(x => x.Id).Distinct().Should().Equal(ids[0]);
    }

    [Fact(DisplayName =
        "When a playlist is matched on publish time, then a video published 20 hours after the episode's release plus delay matches on any host zone, which pins the explicit UTC conversion; Episode.ReleaseUtc is already UTC kind, so this rule does not fail if that conversion is reverted.")]
    public void playlist_publish_time_match_reads_release_as_utc()
    {
        // Arrange
        // Episode.ReleaseUtc stores UTC whatever the kind it is given; the finder must not re-read it as local.
        var release = CreateUnspecifiedUtcInstant();
        var delay = TimeSpan.FromMinutes(_fixture.Create<int>() % 120 + 1);
        var episode = _fixture.CreateEpisode();
        episode.ReleaseUtc = release;
        var item = new PlaylistItem
        {
            Snippet = new PlaylistItemSnippet { PublishedAtDateTimeOffset = AsUtc(release).Add(delay).AddHours(20) }
        };

        // Act
        var match = PlaylistItemFinder.MatchOnPublishTimeComparedToPublishDelay(episode, [item], delay);

        // Assert
        match.Should().BeSameAs(item);
    }

    [Fact(DisplayName =
        "When submit-time channel uploads are filtered to within two days of an unspecified-kind expected publish time, then an upload 47 hours after it is kept, because the expected publish time is UTC and reading it as host-local time east of UTC pushes the upload outside the window.")]
    public void submit_publish_window_reads_expected_publish_as_utc()
    {
        // Arrange
        var expectedPublish = CreateUnspecifiedUtcInstant();
        var inside = new PlaylistItem
        {
            Snippet = new PlaylistItemSnippet { PublishedAtDateTimeOffset = AsUtc(expectedPublish).AddHours(47) }
        };
        var outside = new PlaylistItem
        {
            Snippet = new PlaylistItemSnippet { PublishedAtDateTimeOffset = AsUtc(expectedPublish).AddHours(-49) }
        };

        // Act
        var within = YouTubeUrlCategoriser.PublishedWithinThreshold([inside, outside], expectedPublish);

        // Assert
        within.Should().Equal(inside);
    }

    [YouTubeReleaseTimeGuardChildFact(DisplayName =
        "When this class runs in the guard's child test host, then the host really is on Pacific/Auckland time, because a missing tzdata would silently fall back to UTC and every rule would pass without proving anything.")]
    public void child_host_is_east_of_utc()
    {
        // Arrange
        var local = TimeZoneInfo.Local;

        // Act
        var standardOffset = local.BaseUtcOffset;

        // Assert
        standardOffset.Should().Be(TimeSpan.FromHours(12),
            "TZ={0} should give NZST; local zone was '{1}'", EastOfUtcChildTestHost.EastOfUtcZone, local.Id);
    }
}

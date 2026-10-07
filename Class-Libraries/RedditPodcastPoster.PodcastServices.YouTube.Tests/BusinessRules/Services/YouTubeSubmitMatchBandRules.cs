using FluentAssertions;
using Google.Apis.YouTube.v3.Data;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Episodes.Matching;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.PodcastServices.YouTube.ChannelSnippets;
using RedditPodcastPoster.PodcastServices.YouTube.Models;
using RedditPodcastPoster.PodcastServices.YouTube.Playlist;
using RedditPodcastPoster.PodcastServices.YouTube.Services;

namespace RedditPodcastPoster.PodcastServices.YouTube.Tests.BusinessRules.Services;

/// <summary>
/// Matching a known YouTube channel from a Spotify or Apple submit searches a short publish band.
/// It must not walk the channel uploads or playlist through to now.
/// </summary>
public class YouTubeSubmitMatchBandRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();

    public YouTubeSubmitMatchBandRules()
    {
        _mocker.GetMock<IYouTubeChannelReleaseBandSearch>()
            .Setup(x => x.Search(
                It.IsAny<string>(),
                It.IsAny<DateTimeOffset>(),
                It.IsAny<DateTimeOffset>(),
                It.IsAny<IndexingContext>()))
            .ReturnsAsync(new List<SearchResult>());
    }

    [Fact(DisplayName =
        "When the podcast has a YouTube channel, Resolve searches that channel between the submit-match band edges " +
        "shifted by the publishing delay, and does not walk the playlist, because the audio date is a clue for the video date.")]
    public async Task Known_channel_searches_the_band_around_audio_plus_delay()
    {
        // Arrange
        var channelId = _fixture.CreateYouTubeChannelId();
        var release = DomainTestFixture.UtcDateDaysAgo(1);
        var delay = TimeSpan.FromDays(3);
        var podcast = _fixture.CreatePodcast(p =>
        {
            p.YouTubeChannelId = channelId;
            p.YouTubePlaylistId = _fixture.CreateYouTubePlaylistId();
            p.YouTubePublicationOffset = delay.Ticks;
        });
        DateTimeOffset? publishedAfter = null;
        DateTimeOffset? publishedBefore = null;
        _mocker.GetMock<IYouTubeChannelReleaseBandSearch>()
            .Setup(x => x.Search(
                channelId,
                It.IsAny<DateTimeOffset>(),
                It.IsAny<DateTimeOffset>(),
                It.IsAny<IndexingContext>()))
            .Callback<string, DateTimeOffset, DateTimeOffset, IndexingContext>((_, after, before, _) =>
            {
                publishedAfter = after;
                publishedBefore = before;
            })
            .ReturnsAsync(new List<SearchResult>());
        var sut = _mocker.CreateInstance<YouTubeUrlCategoriser>();
        var expectedPublish = release.Add(delay);
        var band = EpisodeReleaseTolerance.GetSubmitMatchBand(expectedPublish);

        // Act
        var result = await sut.Resolve(CreateCriteria(release), podcast, [], new IndexingContext());

        // Assert
        result.Should().BeNull();
        publishedAfter.Should().Be(new DateTimeOffset(DateTime.SpecifyKind(band.Start, DateTimeKind.Utc)));
        publishedBefore.Should().Be(new DateTimeOffset(DateTime.SpecifyKind(band.End.AddDays(1), DateTimeKind.Utc)));
        _mocker.GetMock<ITolerantYouTubePlaylistService>().Verify(
            x => x.GetPlaylistVideoSnippets(
                It.IsAny<YouTubePlaylistId>(),
                It.IsAny<IndexingContext>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<PlaylistOrder?>()),
            Times.Never);
    }

    [Fact(DisplayName =
        "When the audio release is years old and the channel is known, Resolve still bounds publishedBefore to the band " +
        "because a 2024 episode must not search the channel through to today.")]
    public async Task Years_old_audio_does_not_search_through_to_now()
    {
        // Arrange
        var channelId = _fixture.CreateYouTubeChannelId();
        var release = DomainTestFixture.UtcDateDaysAgo(800);
        var podcast = _fixture.CreatePodcast(p =>
        {
            p.YouTubeChannelId = channelId;
            p.YouTubePublicationOffset = 0;
        });
        DateTimeOffset? publishedBefore = null;
        _mocker.GetMock<IYouTubeChannelReleaseBandSearch>()
            .Setup(x => x.Search(
                channelId,
                It.IsAny<DateTimeOffset>(),
                It.IsAny<DateTimeOffset>(),
                It.IsAny<IndexingContext>()))
            .Callback<string, DateTimeOffset, DateTimeOffset, IndexingContext>((_, _, before, _) =>
                publishedBefore = before)
            .ReturnsAsync(new List<SearchResult>());
        var sut = _mocker.CreateInstance<YouTubeUrlCategoriser>();
        var band = EpisodeReleaseTolerance.GetSubmitMatchBand(release);

        // Act
        await sut.Resolve(CreateCriteria(release), podcast, [], new IndexingContext());

        // Assert
        publishedBefore.Should().Be(new DateTimeOffset(DateTime.SpecifyKind(band.End.AddDays(1), DateTimeKind.Utc)));
        publishedBefore!.Value.UtcDateTime.Should().BeBefore(DateTime.UtcNow.AddDays(-700));
    }

    [Fact(DisplayName =
        "When the podcast has no YouTube channel, Resolve does not search YouTube " +
        "because a Spotify or Apple URL cannot be matched to a video without a channel.")]
    public async Task Missing_channel_does_not_search_youtube()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var sut = _mocker.CreateInstance<YouTubeUrlCategoriser>();

        // Act
        var result = await sut.Resolve(
            CreateCriteria(DomainTestFixture.UtcDateDaysAgo(1)),
            podcast,
            [],
            new IndexingContext());

        // Assert
        result.Should().BeNull();
        _mocker.GetMock<IYouTubeChannelReleaseBandSearch>().Verify(
            x => x.Search(
                It.IsAny<string>(),
                It.IsAny<DateTimeOffset>(),
                It.IsAny<DateTimeOffset>(),
                It.IsAny<IndexingContext>()),
            Times.Never);
    }

    [Fact(DisplayName =
        "The channel release-band search is at most two pages of fifty videos " +
        "because a submit looks around one date and must not page the channel to now.")]
    public void Channel_search_is_capped_at_two_pages()
    {
        // Arrange
        var pageSize = YouTubeChannelReleaseBandSearch.PageSize;
        var maxPages = YouTubeChannelReleaseBandSearch.MaxPages;

        // Act
        var videosExamined = pageSize * maxPages;

        // Assert
        pageSize.Should().Be(50);
        maxPages.Should().Be(2);
        videosExamined.Should().Be(100);
    }

    private PodcastServiceSearchCriteria CreateCriteria(DateTime release) =>
        new(
            ShowName: _fixture.CreateTitle(),
            ShowDescription: _fixture.CreateTitle(),
            Publisher: _fixture.CreateTitle(),
            EpisodeTitle: _fixture.CreateTitle(),
            EpisodeDescription: _fixture.CreateTitle(),
            Release: release,
            Duration: _fixture.CreateDuration());
}

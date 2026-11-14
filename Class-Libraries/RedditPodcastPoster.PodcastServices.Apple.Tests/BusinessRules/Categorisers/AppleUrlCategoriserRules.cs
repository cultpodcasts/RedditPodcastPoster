using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.PodcastServices.Apple.Categorisers;
using RedditPodcastPoster.PodcastServices.Apple.Models;
using RedditPodcastPoster.PodcastServices.Apple.Resolvers;

namespace RedditPodcastPoster.PodcastServices.Apple.Tests.BusinessRules.Categorisers;

/// <summary>
/// MatchOtherServices Apple resolution must still look up Apple while a positive YouTube publishing delay
/// is running for a YouTube-authority podcast, because the audio went out <c>release - offset</c> (#1375).
/// </summary>
public class AppleUrlCategoriserRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();

    public AppleUrlCategoriserRules()
    {
        _mocker.Use<ILogger<AppleUrlCategoriser>>(NullLogger<AppleUrlCategoriser>.Instance);
    }

    [Fact(DisplayName =
        "When a YouTube-authority podcast is still inside the positive publishing delay and the criteria come from YouTube, " +
        "Resolve still calls FindEpisode with Released = criteria release - offset " +
        "because a positive delay means the Apple audio was released before the YouTube video (#1375).")]
    public async Task Resolve_queries_apple_inside_positive_youtube_publishing_delay()
    {
        // Arrange
        var delay = TimeSpan.FromDays(7);
        var podcast = _fixture.CreateYouTubeReleaseAuthorityPodcast(
            _fixture.CreateYouTubeChannelId(),
            delay.Ticks,
            _fixture.CreateSpotifyId());
        podcast.AppleId = _fixture.CreateAppleId();
        var criteria = new PodcastServiceSearchCriteria(
            ShowName: podcast.Name,
            ShowDescription: _fixture.CreateTitle(),
            Publisher: _fixture.CreateTitle(),
            EpisodeTitle: _fixture.CreateTitle(),
            EpisodeDescription: _fixture.CreateTitle(),
            Release: DomainTestFixture.UtcToday,
            Duration: TimeSpan.FromMinutes(60))
        {
            SourceAuthority = Service.YouTube
        };
        _mocker.GetMock<IApplePodcastResolver>()
            .Setup(x => x.FindPodcast(It.IsAny<FindApplePodcastRequest>()))
            .ReturnsAsync(new iTunesSearch.Library.Models.Podcast
            {
                Id = podcast.AppleId.Value,
                Name = podcast.Name
            });
        var requests = new List<FindAppleEpisodeRequest>();
        var episodeResolver = _mocker.GetMock<IAppleEpisodeResolver>();
        episodeResolver
            .Setup(x => x.FindEpisode(
                It.IsAny<FindAppleEpisodeRequest>(),
                It.IsAny<IndexingContext>(),
                It.IsAny<Func<AppleEpisode, bool>?>(),
                It.IsAny<CancellationToken>()))
            .Callback<FindAppleEpisodeRequest, IndexingContext, Func<AppleEpisode, bool>?, CancellationToken>(
                (request, _, _, _) => requests.Add(request))
            .ReturnsAsync((AppleEpisode?)null);
        var sut = _mocker.CreateInstance<AppleUrlCategoriser>();

        // Act
        var result = await sut.Resolve(criteria, podcast, new IndexingContext());

        // Assert
        result.Should().BeNull();
        episodeResolver.Verify(
            x => x.FindEpisode(
                It.IsAny<FindAppleEpisodeRequest>(),
                It.IsAny<IndexingContext>(),
                It.IsAny<Func<AppleEpisode, bool>?>(),
                It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
        requests.Should().NotBeEmpty();
        requests[0].Released.Should().Be(criteria.Release - delay);
    }
}
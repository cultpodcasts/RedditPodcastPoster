using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RedditPodcastPoster.Episodes;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Spotify.Categorisers;
using RedditPodcastPoster.PodcastServices.Spotify.Factories;
using RedditPodcastPoster.PodcastServices.Spotify.Models;
using RedditPodcastPoster.PodcastServices.Spotify.Resolvers;
using RedditPodcastPoster.Text;
using SpotifyAPI.Web;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;

namespace RedditPodcastPoster.PodcastServices.Spotify.Tests.BusinessRules.Categorisers;

/// <summary>
/// MatchOtherServices Spotify resolution must honour expensive-query and delayed-audio guards,
/// retry with AppleTitle when the primary title misses, and pass a GetToleranceTicks release reducer into FindEpisode.
/// </summary>
public class SpotifyUrlCategoriserRules
{
    private readonly DomainTestFixture _fixture = new();

    [Fact(DisplayName =
        "When the matching podcast has an expensive Spotify query and SkipExpensiveSpotifyQueries is set, Resolve returns null without calling FindEpisode " +
        "because MatchOtherServices must not walk high-volume catalogues on guarded passes.")]
    public async Task Resolve_short_circuits_when_expensive_query_guard_set()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast(p =>
        {
            p.SpotifyEpisodesQueryIsExpensive = true;
            p.SpotifyId = _fixture.CreateSpotifyId();
        });
        var criteria = CreateCriteria();
        var resolver = new Mock<ISpotifyEpisodeResolver>(MockBehavior.Strict);
        var sut = CreateSut(resolver.Object);
        var indexingContext = new IndexingContext(SkipExpensiveSpotifyQueries: true);

        // Act
        var result = await sut.Resolve(criteria, podcast, indexingContext);

        // Assert
        result.Should().BeNull();
        resolver.Verify(
            x => x.FindEpisode(
                It.IsAny<FindSpotifyEpisodeRequest>(),
                It.IsAny<IndexingContext>(),
                It.IsAny<Func<SimpleEpisode, bool>?>()),
            Times.Never);
    }

    [Fact(DisplayName =
        "When SkipExpensiveSpotifyQueries is false for an expensive podcast, Resolve still attempts FindEpisode " +
        "because API submit and discovery curation allow expensive Spotify matching.")]
    public async Task Resolve_calls_find_episode_when_expensive_queries_allowed()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast(p =>
        {
            p.SpotifyEpisodesQueryIsExpensive = true;
            p.SpotifyId = _fixture.CreateSpotifyId();
        });
        var criteria = CreateCriteria();
        var resolver = new Mock<ISpotifyEpisodeResolver>();
        resolver
            .Setup(x => x.FindEpisode(
                It.IsAny<FindSpotifyEpisodeRequest>(),
                It.IsAny<IndexingContext>(),
                It.IsAny<Func<SimpleEpisode, bool>?>()))
            .ReturnsAsync(new FindEpisodeResponse(null));
        var sut = CreateSut(resolver.Object);
        var indexingContext = new IndexingContext(SkipExpensiveSpotifyQueries: false);

        // Act
        var result = await sut.Resolve(criteria, podcast, indexingContext);

        // Assert
        result.Should().BeNull();
        resolver.Verify(
            x => x.FindEpisode(
                It.IsAny<FindSpotifyEpisodeRequest>(),
                It.IsAny<IndexingContext>(),
                It.IsAny<Func<SimpleEpisode, bool>?>()),
            Times.AtLeastOnce);
    }

    [Fact(DisplayName =
        "When a YouTube-authority podcast is still inside the positive publishing delay, Resolve returns null without FindEpisode " +
        "because audio is not expected on Spotify until after the delayed-release window.")]
    public async Task Resolve_skips_while_awaiting_delayed_audio_release()
    {
        // Arrange
        var podcast = _fixture.CreateYouTubeReleaseAuthorityPodcast(
            _fixture.CreateYouTubeChannelId(),
            TimeSpan.FromDays(7).Ticks,
            _fixture.CreateSpotifyId());
        podcast.SpotifyEpisodesQueryIsExpensive = false;
        var criteria = CreateCriteria(
            release: DomainTestFixture.UtcToday,
            duration: TimeSpan.FromMinutes(60));
        var resolver = new Mock<ISpotifyEpisodeResolver>(MockBehavior.Strict);
        var sut = CreateSut(resolver.Object);

        // Act
        var result = await sut.Resolve(criteria, podcast, new IndexingContext(SkipExpensiveSpotifyQueries: false));

        // Assert
        result.Should().BeNull();
        resolver.Verify(
            x => x.FindEpisode(
                It.IsAny<FindSpotifyEpisodeRequest>(),
                It.IsAny<IndexingContext>(),
                It.IsAny<Func<SimpleEpisode, bool>?>()),
            Times.Never);
    }

    [Fact(DisplayName =
        "When the primary EpisodeTitle FindEpisode returns null and AppleTitle is set, Resolve retries FindEpisode with AppleTitle " +
        "because Apple and Spotify titles often differ and MatchOtherServices must try both.")]
    public async Task Resolve_retries_find_episode_with_apple_title_when_primary_title_misses()
    {
        // Arrange
        var primaryTitle = _fixture.CreateTitle();
        var appleTitle = _fixture.CreateTitle() + " Apple";
        var podcast = _fixture.CreateSpotifyPrimaryPodcast(_fixture.CreateSpotifyId());
        podcast.SpotifyEpisodesQueryIsExpensive = false;
        var criteria = CreateCriteria(episodeTitle: primaryTitle);
        criteria.AppleTitle = appleTitle;
        var episodeId = _fixture.CreateSpotifyId();
        var fullEpisode = CreateFullEpisode(episodeId, appleTitle);
        var requestedTitles = new List<string>();
        var resolver = new Mock<ISpotifyEpisodeResolver>();
        resolver
            .Setup(x => x.FindEpisode(
                It.IsAny<FindSpotifyEpisodeRequest>(),
                It.IsAny<IndexingContext>(),
                It.IsAny<Func<SimpleEpisode, bool>?>()))
            .Returns<FindSpotifyEpisodeRequest, IndexingContext, Func<SimpleEpisode, bool>?>(
                (request, _, _) =>
                {
                    requestedTitles.Add(request.EpisodeTitle);
                    return Task.FromResult(
                        request.EpisodeTitle == appleTitle
                            ? new FindEpisodeResponse(fullEpisode)
                            : new FindEpisodeResponse(null));
                });
        var sut = CreateSut(resolver.Object);

        // Act
        var result = await sut.Resolve(criteria, podcast, new IndexingContext(SkipExpensiveSpotifyQueries: false));

        // Assert
        result.Should().NotBeNull();
        result!.EpisodeTitle.Should().Be(appleTitle);
        result.EpisodeId.Should().Be(episodeId);
        requestedTitles.Should().Equal(primaryTitle.Trim(), appleTitle.Trim());
        resolver.Verify(
            x => x.FindEpisode(
                It.IsAny<FindSpotifyEpisodeRequest>(),
                It.IsAny<IndexingContext>(),
                It.IsAny<Func<SimpleEpisode, bool>?>()),
            Times.Exactly(2));
    }

    [Fact(DisplayName =
        "When AppleTitle is unset and the primary title FindEpisode returns null, Resolve does not retry FindEpisode " +
        "because there is no alternate Apple title to fall back to.")]
    public async Task Resolve_does_not_retry_when_apple_title_unset()
    {
        // Arrange
        var podcast = _fixture.CreateSpotifyPrimaryPodcast(_fixture.CreateSpotifyId());
        podcast.SpotifyEpisodesQueryIsExpensive = false;
        var criteria = CreateCriteria();
        criteria.AppleTitle = null;
        var resolver = new Mock<ISpotifyEpisodeResolver>();
        resolver
            .Setup(x => x.FindEpisode(
                It.IsAny<FindSpotifyEpisodeRequest>(),
                It.IsAny<IndexingContext>(),
                It.IsAny<Func<SimpleEpisode, bool>?>()))
            .ReturnsAsync(new FindEpisodeResponse(null));
        var sut = CreateSut(resolver.Object);

        // Act
        var result = await sut.Resolve(criteria, podcast, new IndexingContext(SkipExpensiveSpotifyQueries: false));

        // Assert
        result.Should().BeNull();
        resolver.Verify(
            x => x.FindEpisode(
                It.IsAny<FindSpotifyEpisodeRequest>(),
                It.IsAny<IndexingContext>(),
                It.IsAny<Func<SimpleEpisode, bool>?>()),
            Times.Once);
    }

    [Fact(DisplayName =
        "When Resolve looks up Spotify from criteria, FindEpisode receives a reducer that accepts releases within GetToleranceTicks " +
        "and rejects those outside because MatchOtherServices must bound catalogue candidates by release tolerance.")]
    public async Task Resolve_passes_release_tolerance_reducer_from_get_tolerance_ticks()
    {
        // Arrange
        var release = DomainTestFixture.UtcDateDaysAgo(5);
        var duration = TimeSpan.FromMinutes(55);
        var podcast = _fixture.CreateSpotifyPrimaryPodcast(_fixture.CreateSpotifyId());
        podcast.SpotifyEpisodesQueryIsExpensive = false;
        var criteria = CreateCriteria(release: release, duration: duration);
        var factoryRequest = FindSpotifyEpisodeRequestFactory.Create(podcast, criteria);
        var expectedTicks = EpisodeReleaseTolerance.GetToleranceTicks(
            podcast,
            criteria.Duration,
            factoryRequest.YouTubePublishingDelay,
            factoryRequest.ReleaseAuthority);
        Func<SimpleEpisode, bool>? capturedReducer = null;
        DateTime? capturedReleased = null;
        var resolver = new Mock<ISpotifyEpisodeResolver>();
        resolver
            .Setup(x => x.FindEpisode(
                It.IsAny<FindSpotifyEpisodeRequest>(),
                It.IsAny<IndexingContext>(),
                It.IsAny<Func<SimpleEpisode, bool>?>()))
            .Callback<FindSpotifyEpisodeRequest, IndexingContext, Func<SimpleEpisode, bool>?>(
                (request, _, reducer) =>
                {
                    capturedReducer = reducer;
                    capturedReleased = request.Released;
                })
            .ReturnsAsync(new FindEpisodeResponse(null));
        var sut = CreateSut(resolver.Object);

        // Act
        await sut.Resolve(criteria, podcast, new IndexingContext(SkipExpensiveSpotifyQueries: false));

        // Assert
        capturedReducer.Should().NotBeNull();
        capturedReleased.Should().Be(factoryRequest.Released);
        expectedTicks.Should().Be(EpisodeReleaseTolerance.YouTubeAuthorityToAudioReleaseConsiderationThreshold.Ticks);

        var withinTolerance = CreateSimpleEpisode(
            release.AddTicks(expectedTicks - TimeSpan.FromHours(1).Ticks));
        var outsideTolerance = CreateSimpleEpisode(
            release.AddTicks(expectedTicks + TimeSpan.FromHours(1).Ticks));

        capturedReducer!(withinTolerance).Should().BeTrue();
        capturedReducer(outsideTolerance).Should().BeFalse();
    }

    private PodcastServiceSearchCriteria CreateCriteria(
        DateTime? release = null,
        TimeSpan? duration = null,
        string? episodeTitle = null) =>
        new(
            ShowName: _fixture.CreateTitle(),
            ShowDescription: _fixture.CreateTitle(),
            Publisher: _fixture.CreateTitle(),
            EpisodeTitle: episodeTitle ?? _fixture.CreateTitle(),
            EpisodeDescription: _fixture.CreateTitle(),
            Release: release ?? DomainTestFixture.UtcDateDaysAgo(1),
            Duration: duration ?? _fixture.CreateDuration());

    private FullEpisode CreateFullEpisode(string episodeId, string title)
    {
        var showId = _fixture.CreateSpotifyId();
        return new FullEpisode
        {
            Id = episodeId,
            Name = title,
            HtmlDescription = $"<p>{_fixture.CreateTitle()}</p>",
            DurationMs = (int)_fixture.CreateDuration().TotalMilliseconds,
            ReleaseDate = DomainTestFixture.UtcDateDaysAgo(1).ToString("yyyy-MM-dd"),
            Explicit = false,
            IsPlayable = true,
            ExternalUrls = new Dictionary<string, string>
            {
                ["spotify"] = _fixture.DefaultSpotifyUrl(episodeId).ToString()
            },
            Images = [],
            Show = new SimpleShow
            {
                Id = showId,
                Name = _fixture.CreateTitle(),
                Description = _fixture.CreateTitle(),
                // 'publisher' removed from Spotify show objects (Feb 2026); still exercised for pass-through.
#pragma warning disable CS0618
                Publisher = _fixture.CreateTitle()
#pragma warning restore CS0618
            }
        };
    }

    private SimpleEpisode CreateSimpleEpisode(DateTime release) =>
        new()
        {
            Id = _fixture.CreateSpotifyId(),
            Name = _fixture.CreateTitle(),
            DurationMs = (int)_fixture.CreateDuration().TotalMilliseconds,
            ReleaseDate = release.ToString("yyyy-MM-dd"),
            Type = ItemType.Episode,
            IsPlayable = true
        };

    private static SpotifyUrlCategoriser CreateSut(ISpotifyEpisodeResolver resolver) =>
        new(
            resolver,
            new HtmlSanitiser(NullLogger<HtmlSanitiser>.Instance),
            NullLogger<SpotifyUrlCategoriser>.Instance);
}

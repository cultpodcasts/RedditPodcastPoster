using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using RedditPodcastPoster.Episodes.TestSupport;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Apple.Models;
using RedditPodcastPoster.PodcastServices.Apple.Providers;
using RedditPodcastPoster.PodcastServices.Apple.Resolvers;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.PodcastServices.Apple.Tests.Fakes;

namespace RedditPodcastPoster.PodcastServices.Apple.Tests;

/// <summary>
/// Thin-wrapper rules: Apple resolver delegates catalogue matching to the domain matcher.
/// </summary>
public class AppleEpisodeResolverCatalogueWrapperRules
{
    private readonly DomainTestFixture _fixture = new();

    [Fact(DisplayName =
        "When the Apple resolver enriches a YouTube-discovered episode with title confidence and a unique " +
        "duration match, it returns the AppleEpisode mapped from the domain matcher result.")]
    public async Task find_episode_delegates_youtube_discovered_title_confident_match_to_domain_matcher()
    {
        // Arrange — Apple title contains the YouTube episode title (containment confidence)
        var youTubeTitle = _fixture.CreateTitle();
        var probeLength = _fixture.CreateDuration();
        var otherLength = probeLength + TimeSpan.FromMinutes(25);
        var probeRelease = DomainTestFixture.UtcAtTime(-3, _fixture.CreateNonMidnightTimeOfDay());
        var matchingAppleId = _fixture.CreateAppleId();
        var appleEpisodes = new[]
        {
            new AppleEpisode(
                matchingAppleId,
                $"{youTubeTitle}: editorial Apple rename",
                probeRelease.AddHours(-1),
                probeLength + TimeSpan.FromSeconds(20),
                new Uri($"https://podcasts.apple.com/us/podcast/episode/id{_fixture.CreateAppleId()}?i={matchingAppleId}"),
                string.Empty,
                false),
            new AppleEpisode(
                _fixture.CreateAppleId(),
                _fixture.CreateTitle(),
                DomainTestFixture.UtcDateDaysAgo(60),
                otherLength,
                new Uri($"https://podcasts.apple.com/us/podcast/episode/id{_fixture.CreateAppleId()}?i={_fixture.CreateAppleId()}"),
                string.Empty,
                false)
        };
        var request = new FindAppleEpisodeRequest(
            _fixture.CreateAppleId(),
            _fixture.CreateTitle(),
            null,
            youTubeTitle,
            probeRelease,
            null,
            probeLength,
            null,
            EnrichingYouTubeDiscoveredEpisode: true);

        var sut = new AppleEpisodeResolver(
            new StubApplePodcastService(appleEpisodes),
            EpisodeDomainTestServices.CreatePlatformMatcher(),
            new StubSubjectMatcher(),
            NullLogger<AppleEpisodeResolver>.Instance);

        // Act
        var result = await sut.FindEpisode(request, new IndexingContext());

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(matchingAppleId);
    }

    [Fact(DisplayName =
        "When Apple returns a YouTube-discovered catalogue row with zero duration and a title that " +
        "contains the probe title, the resolver still returns that Apple episode, " +
        "because Apple omitting durationInMilliseconds must not block title-confident enrich.")]
    public async Task find_episode_matches_youtube_discovered_when_apple_omits_duration()
    {
        // Arrange
        var youTubeTitle = _fixture.CreateTitle();
        var probeLength = _fixture.CreateDuration();
        var probeRelease = DomainTestFixture.UtcAtTime(-3, _fixture.CreateNonMidnightTimeOfDay());
        var matchingAppleId = _fixture.CreateAppleId();
        var appleEpisodes = new[]
        {
            new AppleEpisode(
                matchingAppleId,
                $"{youTubeTitle}: editorial Apple rename",
                probeRelease.AddHours(-1),
                TimeSpan.Zero,
                new Uri($"https://podcasts.apple.com/us/podcast/episode/id{_fixture.CreateAppleId()}?i={matchingAppleId}"),
                string.Empty,
                false)
        };
        var request = new FindAppleEpisodeRequest(
            _fixture.CreateAppleId(),
            _fixture.CreateTitle(),
            null,
            youTubeTitle,
            probeRelease,
            null,
            probeLength,
            null,
            EnrichingYouTubeDiscoveredEpisode: true);

        var sut = new AppleEpisodeResolver(
            new StubApplePodcastService(appleEpisodes),
            EpisodeDomainTestServices.CreatePlatformMatcher(),
            new StubSubjectMatcher(),
            NullLogger<AppleEpisodeResolver>.Instance);

        // Act
        var result = await sut.FindEpisode(request, new IndexingContext());

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(matchingAppleId);
        result.Duration.Should().Be(TimeSpan.Zero);
    }

    private sealed class StubApplePodcastService(IEnumerable<AppleEpisode> episodes) : ICachedApplePodcastService
    {
        public Task<AppleEpisode?> SingleUseGetEpisode(
            ApplePodcastId podcastId,
            long episodeId,
            IndexingContext indexingContext) =>
            GetEpisode(podcastId, episodeId, indexingContext);

        public Task<AppleEpisode?> GetEpisode(ApplePodcastId podcastId, long episodeId, IndexingContext indexingContext) =>
            Task.FromResult(episodes.FirstOrDefault(x => x.Id == episodeId));

        public Task<IEnumerable<AppleEpisode>?> GetEpisodes(ApplePodcastId podcastId, IndexingContext indexingContext) =>
            Task.FromResult<IEnumerable<AppleEpisode>?>(episodes);
    }
}

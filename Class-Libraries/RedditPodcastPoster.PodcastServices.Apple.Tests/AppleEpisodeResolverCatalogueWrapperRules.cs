using RedditPodcastPoster.Subjects.Matching;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using RedditPodcastPoster.DependencyInjection;
using RedditPodcastPoster.Models.TitleCasing;
using RedditPodcastPoster.Text.TitleCasing;
using RedditPodcastPoster.Episodes.TestSupport;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Apple.Models;
using RedditPodcastPoster.PodcastServices.Apple.Providers;
using RedditPodcastPoster.PodcastServices.Apple.Resolvers;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.PodcastServices.Apple.Tests.Fakes;

using RedditPodcastPoster.PodcastServices.Apple.Tests.Support;

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

        var sut = AppleResolverHarness.Create(appleEpisodes, new CatalogueSubjectClassifier(new StubSubjectMatcher(), EmptyTitleCasingProvider()), EpisodeDomainTestServices.CreatePlatformMatcher()).Resolver;

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

        var sut = AppleResolverHarness.Create(appleEpisodes, new CatalogueSubjectClassifier(new StubSubjectMatcher(), EmptyTitleCasingProvider()), EpisodeDomainTestServices.CreatePlatformMatcher()).Resolver;

        // Act
        var result = await sut.FindEpisode(request, new IndexingContext());

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(matchingAppleId);
        result.Duration.Should().Be(TimeSpan.Zero);
    }

    private static IAsyncInstance<ITitleCasingRulesProvider> EmptyTitleCasingProvider() =>
        new StubAsyncInstance<ITitleCasingRulesProvider>(
            new TitleCasingRulesProvider(
                new Dictionary<string, TitleCasingRulesDocument>(StringComparer.OrdinalIgnoreCase)));

    private sealed class StubAsyncInstance<T>(T value) : IAsyncInstance<T>
    {
        public Task<T> GetAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(value);
    }
}

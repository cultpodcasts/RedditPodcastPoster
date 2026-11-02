using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using RedditPodcastPoster.Episodes.TestSupport;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Episodes.TestSupport.TimeZones;
using RedditPodcastPoster.Models.Serialization;
using RedditPodcastPoster.PodcastServices.Apple.Providers;
using Xunit;

namespace RedditPodcastPoster.PodcastServices.Apple.Tests.BusinessRules.Providers;

/// <summary>
/// iTunes catalogue lookup reads a zone-less releaseDate as UTC (#1219). <see cref="DateTime.ToUniversalTime"/> would
/// treat unspecified as host-local and shift the instant east of UTC. CI is UTC, so
/// <see cref="AppleCatalogueLookupReleaseEastOfUtcHostGuard"/> re-runs this class under TZ=Pacific/Auckland.
/// </summary>
public class AppleCatalogueLookupReleaseUtcRules
{
    private readonly DomainTestFixture _fixture = new();

    private sealed class Handler(string json) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) });
    }

    private sealed class Factory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) =>
            name == AppleCatalogueLookup.HttpClientName
                ? new HttpClient(handler, false)
                : throw new ArgumentException(name);
    }

    private static AppleCatalogueLookup Sut(Handler handler) =>
        new(new Factory(handler), CatalogueTestServices.CreateGuard(), NullLogger<AppleCatalogueLookup>.Instance);

    [Fact(DisplayName =
        "When iTunes returns a zone-less releaseDate, then SearchShowEpisodes maps that wall-clock time as UTC, because an unspecified release is UTC by contract and ToUniversalTime on a host east of UTC would shift it by the host's offset.")]
    public async Task zone_less_release_date_is_read_as_utc()
    {
        // Arrange
        var podcastId = _fixture.CreateAppleId();
        var trackId = _fixture.CreateAppleId();
        var title = _fixture.CreateTitle();
        var release = DomainTestFixture.UtcAtTime(
            -(_fixture.Create<int>() % 300 + 2),
            _fixture.CreateNonMidnightTimeOfDay());
        var zoneLess = DateTime.SpecifyKind(release, DateTimeKind.Unspecified)
            .ToString("yyyy-MM-dd'T'HH:mm:ss");
        var json = JsonSerializer.Serialize(new
        {
            resultCount = 1,
            results = new[]
            {
                new
                {
                    collectionId = podcastId,
                    trackId,
                    trackName = title,
                    releaseDate = zoneLess,
                    trackTimeMillis = (long)_fixture.CreateDuration().TotalMilliseconds,
                    trackViewUrl = $"https://podcasts.apple.com/us/podcast/x/id{podcastId}?i={trackId}"
                }
            }
        });
        var sut = Sut(new Handler(json));

        // Act
        var hits = await sut.SearchShowEpisodes(podcastId, title);

        // Assert
        var hit = hits.Should().ContainSingle().Subject;
        hit.Release.Should().Be(UtcDateTimes.ToUtc(DateTime.SpecifyKind(release, DateTimeKind.Unspecified)));
        hit.Release.Kind.Should().Be(DateTimeKind.Utc);
        hit.Release.Should().Be(DateTime.SpecifyKind(release, DateTimeKind.Utc));
    }

    [AppleCatalogueLookupReleaseGuardChildFact(DisplayName =
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

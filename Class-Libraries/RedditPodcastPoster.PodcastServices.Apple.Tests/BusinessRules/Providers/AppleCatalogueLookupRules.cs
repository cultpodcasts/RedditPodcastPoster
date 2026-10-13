using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using RedditPodcastPoster.Episodes.TestSupport;
using RedditPodcastPoster.PodcastServices.Apple.Providers;

namespace RedditPodcastPoster.PodcastServices.Apple.Tests.BusinessRules.Providers;

/// <summary>
/// Quick iTunes calls (no auth, no quota): the show size from <c>/lookup</c> trackCount and a title search scoped to the
/// show's Apple id by <c>collectionId</c>.
/// </summary>
public class AppleCatalogueLookupRules
{
    private const long ShowId = 1603773245;

    private sealed class Handler(string json, HttpStatusCode status = HttpStatusCode.OK) : HttpMessageHandler
    {
        public List<Uri> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request.RequestUri!);
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(json) });
        }
    }

    private sealed class Factory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) =>
            name == AppleCatalogueLookup.HttpClientName ? new HttpClient(handler, false) : throw new ArgumentException(name);
    }

    private static AppleCatalogueLookup Sut(Handler handler)
    {
        return new AppleCatalogueLookup(new Factory(handler), CatalogueTestServices.CreateGuard(), NullLogger<AppleCatalogueLookup>.Instance);
    }

    [Fact(DisplayName = "GetEpisodeCount reads trackCount from one iTunes lookup and caches it for the pass (Ben McKenzie show: 255).")]
    public async Task Episode_count_from_track_count()
    {
        // Arrange
        var handler = new Handler("""{"resultCount":1,"results":[{"collectionId":1603773245,"trackCount":255}]}""");
        var sut = Sut(handler);

        // Act
        var first = await sut.GetEpisodeCount(ShowId);
        var second = await sut.GetEpisodeCount(ShowId);

        // Assert
        first.Should().Be(255);
        second.Should().Be(255);
        handler.Requests.Should().ContainSingle().Which.Query.Should().Contain($"id={ShowId}");
    }

    [Fact(DisplayName = "A failed iTunes lookup returns null (unknown size) and is not cached.")]
    public async Task Failed_lookup_is_unknown()
    {
        // Arrange
        var handler = new Handler("{}", HttpStatusCode.ServiceUnavailable);
        var sut = Sut(handler);

        // Act
        var count = await sut.GetEpisodeCount(ShowId);
        await sut.GetEpisodeCount(ShowId);

        // Assert
        count.Should().BeNull();
        handler.Requests.Should().HaveCount(2);
    }

    [Fact(DisplayName =
        "SearchShowEpisodes keeps only hits whose collectionId is the show's Apple id and maps trackId, title, release and length.")]
    public async Task Search_is_scoped_to_show()
    {
        // Arrange
        var handler = new Handler("""
            {"resultCount":2,"results":[
              {"collectionId":1603773245,"trackId":1000731000001,"trackName":"Everyone Is Lying to You for Money","releaseDate":"2026-10-12T08:00:00Z","trackTimeMillis":3569000,"trackViewUrl":"https://podcasts.apple.com/us/podcast/x/id1603773245?i=1000731000001&uo=4"},
              {"collectionId":42,"trackId":7,"trackName":"Everyone Is Lying to You for Money","releaseDate":"2026-10-12T08:00:00Z","trackTimeMillis":1000}
            ]}
            """);

        // Act
        var hits = await Sut(handler).SearchShowEpisodes(ShowId, "Everyone Is Lying to You for Money");

        // Assert
        var hit = hits.Should().ContainSingle().Subject;
        hit.Id.Should().Be(1000731000001);
        hit.Release.Should().Be(DateTime.Parse("2026-10-12T08:00:00Z").ToUniversalTime());
        hit.Duration.Should().Be(TimeSpan.FromMilliseconds(3569000));
        handler.Requests.Single().Query.Should().Contain("entity=podcastEpisode");
    }

    [Fact(DisplayName = "A blank title makes no iTunes search.")]
    public async Task Blank_title_makes_no_request()
    {
        // Arrange
        var handler = new Handler("{}");

        // Act
        var hits = await Sut(handler).SearchShowEpisodes(ShowId, "  ");

        // Assert
        hits.Should().BeEmpty();
        handler.Requests.Should().BeEmpty();
    }
}

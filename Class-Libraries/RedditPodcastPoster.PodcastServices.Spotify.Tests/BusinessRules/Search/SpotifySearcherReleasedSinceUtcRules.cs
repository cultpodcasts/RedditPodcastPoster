using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Episodes.TestSupport.TimeZones;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.PodcastServices.Spotify.Client;
using RedditPodcastPoster.PodcastServices.Spotify.Search;
using RedditPodcastPoster.Text.Sanitisers;
using SpotifyAPI.Web;
using Xunit;

namespace RedditPodcastPoster.PodcastServices.Spotify.Tests.BusinessRules.Search;

/// <summary>
/// Spotify discovery search floors <see cref="IndexingContext.ReleasedSince"/> to a UTC calendar day (#1219). An
/// unspecified-kind value is UTC; <see cref="DateTime.ToUniversalTime"/> would read it as host-local and floor to the
/// previous day east of UTC. CI is UTC, so <see cref="SpotifySearcherReleasedSinceEastOfUtcHostGuard"/> re-runs this
/// class under TZ=Pacific/Auckland.
/// </summary>
public class SpotifySearcherReleasedSinceUtcRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();

    public SpotifySearcherReleasedSinceUtcRules()
    {
        _mocker.Use<IHtmlSanitiser>(new HtmlSanitiser(NullLogger<HtmlSanitiser>.Instance));
        _mocker.Use<ILogger<SpotifySearcher>>(NullLogger<SpotifySearcher>.Instance);
    }

    [Fact(DisplayName =
        "When ReleasedSince is an unspecified-kind midnight, then Search keeps an episode on that UTC calendar day and drops one on the previous day, because the day floor treats unspecified as UTC and ToUniversalTime on a host east of UTC would floor to the previous day and widen the window.")]
    public async Task unspecified_released_since_floors_to_utc_calendar_day()
    {
        // Arrange
        var query = _fixture.CreateTitle(1);
        var day = DomainTestFixture.UtcDateDaysAgo(2);
        var previousDay = day.AddDays(-1);
        var since = DateTime.SpecifyKind(day, DateTimeKind.Unspecified);
        var sameDayId = _fixture.CreateSpotifyId();
        var previousDayId = _fixture.CreateSpotifyId();
        var sameDayHit = CreateSimpleEpisode(sameDayId, $"Talk of {query}", day);
        var previousDayHit = CreateSimpleEpisode(previousDayId, $"Talk of {query}", previousDay);
        var sameDayFull = CreateFullEpisode(sameDayId, sameDayHit.Name, day);
        var previousDayFull = CreateFullEpisode(previousDayId, previousDayHit.Name, previousDay);
        DateTime? capturedReleasedSince = null;
        SetupClientReturning(
            [sameDayHit, previousDayHit],
            [sameDayFull, previousDayFull],
            ctx => capturedReleasedSince = ctx.ReleasedSince);
        var sut = _mocker.CreateInstance<SpotifySearcher>();

        // Act
        var results = await sut.Search(query, new IndexingContext(ReleasedSince: since));

        // Assert
        capturedReleasedSince.Should().Be(day);
        results.Should().ContainSingle().Which.Id.Should().Be(sameDayId);
    }

    [SpotifySearcherReleasedSinceGuardChildFact(DisplayName =
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

    private void SetupClientReturning(
        IList<SimpleEpisode> paginateResults,
        IReadOnlyList<FullEpisode> fullEpisodes,
        Action<IndexingContext>? onIndexingContext = null)
    {
        var byId = fullEpisodes.ToDictionary(e => e.Id);
        var client = _mocker.GetMock<ISpotifyClientWrapper>();
        client
            .Setup(x => x.FindEpisodes(
                It.IsAny<SearchRequest>(),
                It.IsAny<IndexingContext>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((SearchRequest _, IndexingContext ctx, CancellationToken _) =>
            {
                onIndexingContext?.Invoke(ctx);
                return new Paging<SimpleEpisode, SearchResponse> { Items = paginateResults.ToList() };
            });
        client
            .Setup(x => x.PaginateAll(
                It.IsAny<IPaginatable<SimpleEpisode, SearchResponse>>(),
                It.IsAny<Func<SearchResponse, IPaginatable<SimpleEpisode, SearchResponse>>>(),
                It.IsAny<IndexingContext>()))
            .ReturnsAsync((
                IPaginatable<SimpleEpisode, SearchResponse> _,
                Func<SearchResponse, IPaginatable<SimpleEpisode, SearchResponse>> _,
                IndexingContext ctx) =>
            {
                onIndexingContext?.Invoke(ctx);
                return paginateResults;
            });
        client
            .Setup(x => x.GetSeveral(
                It.IsAny<EpisodesRequest>(),
                It.IsAny<IndexingContext>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((EpisodesRequest request, IndexingContext _, CancellationToken _) =>
                new EpisodesResponse
                {
                    Episodes = request.Ids
                        .Where(byId.ContainsKey)
                        .Select(id => byId[id])
                        .ToList()
                });
    }

    private SimpleEpisode CreateSimpleEpisode(string id, string name, DateTime release) =>
        new()
        {
            Id = id,
            Name = name,
            Description = _fixture.CreateTitle(),
            DurationMs = (int)_fixture.CreateDuration().TotalMilliseconds,
            ReleaseDate = release.ToString("yyyy-MM-dd"),
            Type = ItemType.Episode,
            IsPlayable = true
        };

    private FullEpisode CreateFullEpisode(string episodeId, string title, DateTime release)
    {
        var showId = _fixture.CreateSpotifyId();
        return new FullEpisode
        {
            Id = episodeId,
            Name = title,
            HtmlDescription = $"<p>{_fixture.CreateTitle()}</p>",
            DurationMs = (int)_fixture.CreateDuration().TotalMilliseconds,
            ReleaseDate = release.ToString("yyyy-MM-dd"),
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
#pragma warning disable CS0618
                Publisher = _fixture.CreateTitle()
#pragma warning restore CS0618
            }
        };
    }
}

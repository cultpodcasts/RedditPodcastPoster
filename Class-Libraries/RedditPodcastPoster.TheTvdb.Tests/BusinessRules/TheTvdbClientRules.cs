using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq.AutoMock;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.TheTvdb.Clients;
using RedditPodcastPoster.TheTvdb.Configuration;
using RedditPodcastPoster.TheTvdb.Models;

namespace RedditPodcastPoster.TheTvdb.Tests.BusinessRules;

public class TheTvdbClientRules
{
    private readonly AutoMocker _mocker = new();
    private readonly DomainTestFixture _fixture = new();
    private readonly StubHandler _handler = new();

    public TheTvdbClientRules()
    {
        var http = new HttpClient(_handler) { BaseAddress = new Uri("https://api4.thetvdb.com/v4/") };
        _mocker.Use(http);
        _mocker.Use(Options.Create(new TheTvdbOptions { ApiKey = _fixture.CreateYouTubeId() }));
    }

    [Fact(DisplayName =
        "A TheTVDB series search hit maps the slug and IMDb remote id onto the public series page and the IMDb title page.")]
    public async Task series_search_maps_canonical_urls()
    {
        // Arrange
        var name = _fixture.CreateTitle();
        var slug = _fixture.CreateYouTubeId();
        var seriesId = _fixture.CreateAppleId();
        var imdbId = "tt" + _fixture.CreateAppleId();
        _handler.Enqueue(HttpStatusCode.OK, LoginJson());
        _handler.Enqueue(HttpStatusCode.OK, SearchJson(name, slug, seriesId, "series", imdbId));
        var sut = _mocker.CreateInstance<TheTvdbClient>();

        // Act
        var hits = await sut.SearchAsync(name, TheTvdbSearchType.Series);

        // Assert
        hits.Should().ContainSingle();
        hits[0].Id.Should().Be(seriesId);
        hits[0].Name.Should().Be(name);
        hits[0].Type.Should().Be(TheTvdbSearchType.Series);
        hits[0].CanonicalUrl.Should().Be(new Uri($"https://www.thetvdb.com/series/{slug}"));
        hits[0].ImdbUrl.Should().Be(new Uri($"https://www.imdb.com/title/{imdbId}/"));
    }

    [Fact(DisplayName =
        "A TheTVDB movie search hit maps the slug onto the public movie page, because a film is not a series.")]
    public async Task movie_search_maps_movie_page()
    {
        // Arrange
        var name = _fixture.CreateTitle();
        var slug = _fixture.CreateYouTubeId();
        var movieId = _fixture.CreateAppleId();
        _handler.Enqueue(HttpStatusCode.OK, LoginJson());
        _handler.Enqueue(HttpStatusCode.OK, SearchJson(name, slug, movieId, "movie", imdbId: null));
        var sut = _mocker.CreateInstance<TheTvdbClient>();

        // Act
        var hits = await sut.SearchAsync(name, TheTvdbSearchType.Movie);

        // Assert
        hits.Should().ContainSingle();
        hits[0].CanonicalUrl.Should().Be(new Uri($"https://www.thetvdb.com/movies/{slug}"));
        hits[0].ImdbUrl.Should().BeNull();
    }

    [Fact(DisplayName =
        "TheTVDB login omits pin when it is unset, because only a subscriber-supported key sends a pin.")]
    public async Task login_omits_empty_pin()
    {
        // Arrange
        _handler.Enqueue(HttpStatusCode.OK, LoginJson());
        _handler.Enqueue(HttpStatusCode.OK, """{"data":[]}""");
        var sut = _mocker.CreateInstance<TheTvdbClient>();

        // Act
        await sut.SearchAsync(_fixture.CreateTitle(), TheTvdbSearchType.Series);

        // Assert
        using var login = JsonDocument.Parse(_handler.Bodies[0]!);
        login.RootElement.TryGetProperty("pin", out _).Should().BeFalse();
        login.RootElement.GetProperty("apikey").GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact(DisplayName =
        "A TheTVDB episode list uses the series slug and episode id for the episode page, not the series page.")]
    public async Task episode_list_maps_episode_page()
    {
        // Arrange
        var seriesId = _fixture.CreateAppleId();
        var episodeId = _fixture.CreateAppleId();
        var slug = _fixture.CreateYouTubeId();
        var episodeName = _fixture.CreateTitle();
        _handler.Enqueue(HttpStatusCode.OK, LoginJson());
        _handler.Enqueue(HttpStatusCode.OK, EpisodePageJson(seriesId, slug, episodeId, episodeName));
        _handler.Enqueue(HttpStatusCode.OK, """{"data":{"series":{},"episodes":[]}}""");
        var sut = _mocker.CreateInstance<TheTvdbClient>();

        // Act
        var episodes = await sut.GetEpisodesAsync(seriesId);

        // Assert
        episodes.Should().ContainSingle();
        episodes[0].Id.Should().Be(episodeId);
        episodes[0].Name.Should().Be(episodeName);
        episodes[0].CanonicalUrl.Should().Be(
            new Uri($"https://www.thetvdb.com/series/{slug}/episodes/{episodeId}"));
    }

    private static string LoginJson() => """{"data":{"token":"issued-token"}}""";

    private static string SearchJson(string name, string slug, long id, string type, string? imdbId)
    {
        var remoteIds = imdbId is null
            ? new JsonArray()
            : new JsonArray { new JsonObject { ["id"] = imdbId, ["sourceName"] = "IMDB" } };
        var payload = new JsonObject
        {
            ["data"] = new JsonArray
            {
                new JsonObject
                {
                    ["name"] = name,
                    ["slug"] = slug,
                    ["tvdb_id"] = id.ToString(),
                    ["type"] = type,
                    ["remote_ids"] = remoteIds
                }
            }
        };
        return payload.ToJsonString();
    }

    private static string EpisodePageJson(long seriesId, string slug, long episodeId, string name)
    {
        var payload = new JsonObject
        {
            ["data"] = new JsonObject
            {
                ["series"] = new JsonObject { ["id"] = seriesId, ["slug"] = slug },
                ["episodes"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["id"] = episodeId,
                        ["seriesId"] = seriesId,
                        ["name"] = name,
                        ["seasonNumber"] = 1,
                        ["number"] = 1
                    }
                }
            }
        };
        return payload.ToJsonString();
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses = new();

        public List<HttpRequestMessage> Requests { get; } = [];

        public List<string?> Bodies { get; } = [];

        public StubHandler Enqueue(HttpStatusCode status, string json)
        {
            _responses.Enqueue(new HttpResponseMessage(status)
            {
                Content = new StringContent(json)
            });
            return this;
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);
            Bodies.Add(request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken));
            return _responses.Dequeue();
        }
    }
}

using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq.AutoMock;
using RedditPodcastPoster.Catalogue.Authority.Imdb;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.PodcastServices.Tests.BusinessRules.Fixtures;

namespace RedditPodcastPoster.PodcastServices.Tests.BusinessRules;

public class ImdbSuggestionClientRules
{
    private readonly AutoMocker _mocker = new();
    private readonly DomainTestFixture _fixture = new();
    private readonly StubHandler _handler = new();

    public ImdbSuggestionClientRules()
    {
        _mocker.Use(new HttpClient(_handler) { BaseAddress = ImdbSuggestionClient.BaseAddress });
    }

    [Fact(DisplayName =
        "IMDb suggestions for an original-language title return the IMDb film found by its alternative title, " +
        "with its id, English title, type, year, poster, and cast, and drop name rows (issue #1421).")]
    public async Task faderhuset_suggestions_map_title_rows()
    {
        // Arrange
        _handler.Respond(HttpStatusCode.OK, FaderhusetFilm.ImdbSuggestionJson);
        var sut = _mocker.CreateInstance<ImdbSuggestionClient>();

        // Act
        var suggestions = await sut.SuggestAsync(FaderhusetFilm.Query);

        // Assert
        suggestions.Should().HaveCount(4);
        suggestions.Should().OnlyContain(s => s.ImdbId.StartsWith("tt"));
        var film = suggestions[0];
        film.ImdbId.Should().Be(FaderhusetFilm.ImdbId);
        film.Title.Should().Be(FaderhusetFilm.ImdbTitle);
        film.Type.Should().Be(ImdbSuggestion.MovieType);
        film.IsFilm.Should().BeTrue();
        film.Year.Should().Be(FaderhusetFilm.Year);
        film.ImageUrl.Should().NotBeNull();
        film.Cast.Should().Be(FaderhusetFilm.Cast);
        suggestions[1].IsFilm.Should().BeFalse();
        _handler.Requests.Should().ContainSingle()
            .Which.RequestUri!.AbsoluteUri.Should().Be(
                $"https://v3.sg.media-imdb.com/suggestion/f/{FaderhusetFilm.Query}.json");
    }

    [Fact(DisplayName =
        "A failed IMDb suggestion call returns no suggestions and logs a warning, because IMDb is only a fallback " +
        "and must not break an identity lookup.")]
    public async Task failed_call_returns_no_suggestions()
    {
        // Arrange
        _handler.Respond(HttpStatusCode.ServiceUnavailable, "{}");
        var sut = _mocker.CreateInstance<ImdbSuggestionClient>();

        // Act
        var suggestions = await sut.SuggestAsync(_fixture.CreateTitle());

        // Assert
        suggestions.Should().BeEmpty();
        _mocker.GetMock<ILogger<ImdbSuggestionClient>>().Invocations
            .Should().Contain(invocation =>
                invocation.Method.Name == nameof(ILogger.Log) &&
                (LogLevel)invocation.Arguments[0] == LogLevel.Warning);
    }

    [Fact(DisplayName =
        "An unreadable IMDb suggestion body returns no suggestions rather than failing the lookup.")]
    public async Task unreadable_body_returns_no_suggestions()
    {
        // Arrange
        _handler.Respond(HttpStatusCode.OK, "<html>");
        var sut = _mocker.CreateInstance<ImdbSuggestionClient>();

        // Act
        var suggestions = await sut.SuggestAsync(_fixture.CreateTitle());

        // Assert
        suggestions.Should().BeEmpty();
    }

    [Fact(DisplayName = "A blank query does not call IMDb.")]
    public async Task blank_query_does_not_call()
    {
        // Arrange
        var sut = _mocker.CreateInstance<ImdbSuggestionClient>();

        // Act
        var suggestions = await sut.SuggestAsync("  ");

        // Assert
        suggestions.Should().BeEmpty();
        _handler.Requests.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "A query that does not start with a letter uses the x bucket, and the query is URL-escaped.")]
    public void non_letter_query_uses_x_bucket()
    {
        // Arrange
        var query = "1 " + _fixture.CreateTitle();

        // Act
        var path = ImdbSuggestionClient.SuggestionPath(query);

        // Assert
        path.Should().Be($"suggestion/x/{Uri.EscapeDataString(query)}.json");
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private HttpStatusCode _status = HttpStatusCode.OK;
        private string _body = "{}";

        public List<HttpRequestMessage> Requests { get; } = [];

        public StubHandler Respond(HttpStatusCode status, string body)
        {
            _status = status;
            _body = body;
            return this;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(new HttpResponseMessage(_status)
            {
                Content = new StringContent(_body, System.Text.Encoding.UTF8, "application/json")
            });
        }
    }
}

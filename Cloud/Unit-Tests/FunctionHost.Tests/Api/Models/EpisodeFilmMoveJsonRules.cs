using System.Text.Json;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using FluentAssertions;
using Api.Dtos.Extensions;
using Api.Models;
using Xunit;

namespace FunctionHost.Tests.Api.Models;

public class EpisodeFilmMoveJsonRules
{
    private readonly DomainTestFixture _fixture = new();

    [Fact(DisplayName =
        "The move-to-film body binds 'title' onto the command request; a missing body maps to a null title, which keeps the episode title.")]
    public void request_title_binds_and_missing_body_is_null_title()
    {
        // Arrange
        var title = _fixture.Create<string>();
        var json = JsonSerializer.Serialize(new { title });

        // Act
        var request = JsonSerializer.Deserialize<global::Api.Dtos.EpisodeFilmMoveRequest>(json).ToModel();
        var missing = ((global::Api.Dtos.EpisodeFilmMoveRequest?)null).ToModel();

        // Assert
        request.Title.Should().Be(title);
        missing.Title.Should().BeNull();
    }

    [Fact(DisplayName =
        "The move-to-film body binds identityDecision and selectedIdentityKey for the ambiguous-identity re-POST (same names as URL-submit).")]
    public void request_identity_decision_binds()
    {
        // Arrange
        var key = "tmdb:" + _fixture.Create<int>();
        var json = JsonSerializer.Serialize(new { identityDecision = "select", selectedIdentityKey = key });

        // Act
        var request = JsonSerializer.Deserialize<global::Api.Dtos.EpisodeFilmMoveRequest>(json).ToModel();

        // Assert
        request.IdentityDecision.Should().Be("select");
        request.SelectedIdentityKey.Should().Be(key);
    }

    [Fact(DisplayName =
        "The move-to-film response carries filmId, name and failureIndexing, and omits a null error, as the website reads them.")]
    public void response_wire_names()
    {
        // Arrange
        var result = new EpisodeFilmMoveResult(
            EpisodeFilmMoveStatus.Accepted,
            _fixture.CreateGuid(),
            _fixture.Create<string>());

        // Act
        var json = JsonSerializer.Serialize(result.ToDto());

        // Assert
        using var document = JsonDocument.Parse(json);
        document.RootElement.GetProperty("filmId").GetGuid().Should().Be(result.FilmId!.Value);
        document.RootElement.GetProperty("name").GetString().Should().Be(result.Name);
        document.RootElement.GetProperty("failureIndexing").GetBoolean().Should().BeFalse();
        document.RootElement.TryGetProperty("error", out _).Should().BeFalse();
        document.RootElement.TryGetProperty("identityDecision", out _).Should().BeFalse();
    }

    [Fact(DisplayName =
        "An IdentityDecisionRequired move response serialises identityDecision.candidates with key/title (URL-submit shape) and omits filmId.")]
    public void response_identity_decision_wire_names()
    {
        // Arrange
        var title = _fixture.CreateTitle();
        var key = "tmdb:" + _fixture.Create<int>();
        var hold = new RedditPodcastPoster.Catalogue.Authority.CatalogueIdentityHold(
            RedditPodcastPoster.Catalogue.Authority.CatalogueAuthorityCandidate.FilmKind,
            title,
            [
                new RedditPodcastPoster.Catalogue.Authority.CatalogueAuthorityCandidate(
                    key,
                    title,
                    Year: null,
                    RedditPodcastPoster.Catalogue.Authority.CatalogueAuthorityCandidate.FilmKind,
                    RedditPodcastPoster.Catalogue.Authority.CatalogueAuthorityCandidate.TmdbSource,
                    1,
                    TvdbId: null)
            ]);
        var result = new EpisodeFilmMoveResult(
            EpisodeFilmMoveStatus.IdentityDecisionRequired,
            IdentityHold: hold);

        // Act
        var json = JsonSerializer.Serialize(result.ToDto());

        // Assert
        using var document = JsonDocument.Parse(json);
        document.RootElement.TryGetProperty("filmId", out var filmId).Should().BeTrue();
        filmId.ValueKind.Should().Be(JsonValueKind.Null);
        var decision = document.RootElement.GetProperty("identityDecision");
        decision.GetProperty("query").GetString().Should().Be(title);
        decision.GetProperty("candidates")[0].GetProperty("key").GetString().Should().Be(key);
        document.RootElement.TryGetProperty("cancelled", out _).Should().BeFalse();
    }
}

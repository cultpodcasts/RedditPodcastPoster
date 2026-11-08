using System.Text;
using System.Text.Json;
using Azure.Core.Serialization;
using FluentAssertions;
using Xunit;

namespace FunctionHost.Tests.Api.Models;

/// <summary>
/// Characterises Isolated <c>[FromBody] Api.Dtos.FilmChangeRequest</c> binding for <c>tmdbId</c>.
/// Isolated HTTP deserialises the body with <see cref="JsonObjectSerializer"/> wrapping the worker default
/// <see cref="JsonSerializerOptions"/> (<c>PropertyNameCaseInsensitive = true</c>). Values that cannot convert
/// to <c>int?</c> throw <see cref="JsonException"/> before <c>PostFilmHandler</c> runs; without
/// <c>FunctionInputBindingBadRequestMiddleware</c> the host would return 500. The HTTP envelope still requires
/// Core Tools or a deployed worker to assert end to end.
/// </summary>
public class FilmChangeRequestFromBodyBindingTests
{
    private static readonly JsonObjectSerializer IsolatedSerializer = new(new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true
    });

    [Fact(DisplayName =
        "Isolated [FromBody] FilmChangeRequest: tmdbId above Int32.MaxValue fails JsonObjectSerializer bind " +
        "with JsonException naming Int32, because the value cannot convert to nullable int before the handler runs.")]
    public async Task tmdb_id_above_int32_max_throws_json_exception()
    {
        // Arrange
        // Boundary: Int32.MaxValue + 1 cannot bind to int? (issue #1231).
        const string json = "{\"tmdbId\":2147483648}";

        // Act
        var act = () => BindAsync(json);

        // Assert
        var error = await act.Should().ThrowAsync<JsonException>();
        error.Which.Message.Should().Contain("Int32");
    }

    [Fact(DisplayName =
        "Isolated [FromBody] FilmChangeRequest: non-integer tmdbId 1.5 fails JsonObjectSerializer bind " +
        "with JsonException, because only whole numbers convert to nullable int.")]
    public async Task tmdb_id_fractional_throws_json_exception()
    {
        // Arrange
        const string json = "{\"tmdbId\":1.5}";

        // Act
        var act = () => BindAsync(json);

        // Assert
        await act.Should().ThrowAsync<JsonException>();
    }

    [Fact(DisplayName =
        "Isolated [FromBody] FilmChangeRequest: string tmdbId fails JsonObjectSerializer bind " +
        "with JsonException, because a JSON string is not a nullable int.")]
    public async Task tmdb_id_string_throws_json_exception()
    {
        // Arrange
        const string json = "{\"tmdbId\":\"7\"}";

        // Act
        var act = () => BindAsync(json);

        // Assert
        await act.Should().ThrowAsync<JsonException>();
    }

    [Fact(DisplayName =
        "Isolated [FromBody] FilmChangeRequest: tmdbId at Int32.MaxValue binds successfully, " +
        "because that value is still within int32.")]
    public async Task tmdb_id_at_int32_max_binds()
    {
        // Arrange
        const string json = "{\"tmdbId\":2147483647}";

        // Act
        var bound = await BindAsync(json);

        // Assert
        bound.TmdbId.Should().Be(int.MaxValue);
    }

    [Fact(DisplayName =
        "Isolated [FromBody] FilmChangeRequest: tmdbId -1 binds successfully, " +
        "because negative values are a domain 400 after bind, not a JSON conversion failure.")]
    public async Task tmdb_id_negative_one_binds()
    {
        // Arrange
        const string json = "{\"tmdbId\":-1}";

        // Act
        var bound = await BindAsync(json);

        // Assert
        bound.TmdbId.Should().Be(-1);
    }

    private static async Task<global::Api.Dtos.FilmChangeRequest> BindAsync(string json)
    {
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        var bound = await IsolatedSerializer.DeserializeAsync(
            stream,
            typeof(global::Api.Dtos.FilmChangeRequest),
            CancellationToken.None);
        return (global::Api.Dtos.FilmChangeRequest)bound!;
    }
}

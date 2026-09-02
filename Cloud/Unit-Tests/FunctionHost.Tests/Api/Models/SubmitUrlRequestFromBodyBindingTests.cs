using System.Text;
using System.Text.Json;
using Azure.Core.Serialization;
using FluentAssertions;
using Api.Models;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using Xunit;

namespace FunctionHost.Tests.Api.Models;

/// <summary>
/// Characterises Isolated <c>[FromBody] SubmitUrlRequest</c> binding.
/// Isolated HTTP (no ASP.NET Core integration in this host) deserialises the body with
/// <see cref="JsonObjectSerializer"/> wrapping the worker default
/// <see cref="JsonSerializerOptions"/> (<c>PropertyNameCaseInsensitive = true</c>),
/// which WorkerOptionsSetup assigns to WorkerOptions.Serializer and
/// DefaultFromBodyConversionFeature uses. There is no function-host /
/// WebApplicationFactory stack in this repo, so these tests pin that bind outcome
/// before <c>PostSubmitUrlHandler</c>. The HTTP envelope (status + Functions error
/// wrapper) requires Core Tools or a deployed worker.
/// </summary>
public class SubmitUrlRequestFromBodyBindingTests
{
    private readonly DomainTestFixture _fixture = new();
    private static readonly JsonObjectSerializer IsolatedSerializer = new(new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true
    });

    [Fact(DisplayName =
        "Isolated [FromBody] SubmitUrlRequest: JSON object with no url property fails JsonObjectSerializer bind " +
        "with JsonException naming Url, because required Uri is missing before the handler runs.")]
    public async Task missing_url_property_throws_json_exception_naming_url()
    {
        // Arrange
        var json = "{}";

        // Act
        var act = () => BindAsync(json);

        // Assert
        var error = await act.Should().ThrowAsync<JsonException>();
        error.Which.Message.Should().Contain("Url");
        error.Which.Message.Should().Contain("required");
    }

    [Fact(DisplayName =
        "Isolated [FromBody] SubmitUrlRequest: podcastName without url fails JsonObjectSerializer bind " +
        "with JsonException naming Url, because required Uri is still missing.")]
    public async Task podcast_name_without_url_throws_json_exception_naming_url()
    {
        // Arrange
        var podcastName = _fixture.CreateTitle();
        var json = JsonSerializer.Serialize(new { podcastName });

        // Act
        var act = () => BindAsync(json);

        // Assert
        var error = await act.Should().ThrowAsync<JsonException>();
        error.Which.Message.Should().Contain("Url");
        error.Which.Message.Should().Contain("required");
    }

    [Fact(DisplayName =
        "Isolated [FromBody] SubmitUrlRequest: url empty string binds a relative empty Uri, " +
        "because System.Text.Json Uri conversion uses RelativeOrAbsolute and does not 400.")]
    public async Task empty_url_string_binds_relative_empty_uri()
    {
        // Arrange
        const string json = """{"url":""}""";

        // Act
        var bound = await BindAsync(json);

        // Assert
        bound.Url.IsAbsoluteUri.Should().BeFalse();
        bound.Url.OriginalString.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "Isolated [FromBody] SubmitUrlRequest: url null binds with a null Url reference, " +
        "because JSON null satisfies required-property presence and Uri conversion allows null at runtime.")]
    public async Task null_url_binds_null_uri_reference()
    {
        // Arrange
        const string json = """{"url":null}""";

        // Act
        var bound = await BindAsync(json);

        // Assert
        bound.Url.Should().BeNull();
    }

    [Fact(DisplayName =
        "Isolated [FromBody] SubmitUrlRequest: url that is not a URI binds as a relative Uri, " +
        "because System.Text.Json Uri conversion accepts RelativeOrAbsolute strings without a scheme.")]
    public async Task invalid_url_string_binds_as_relative_uri()
    {
        // Arrange
        const string json = """{"url":"not-a-uri"}""";

        // Act
        var bound = await BindAsync(json);

        // Assert
        bound.Url.IsAbsoluteUri.Should().BeFalse();
        bound.Url.ToString().Should().Be("not-a-uri");
    }

    [Fact(DisplayName =
        "Isolated [FromBody] SubmitUrlRequest: relative url path is bound as a relative Uri, " +
        "because System.Text.Json Uri conversion accepts RelativeOrAbsolute.")]
    public async Task relative_url_binds_as_relative_uri()
    {
        // Arrange
        const string json = """{"url":"/foo"}""";

        // Act
        var bound = await BindAsync(json);

        // Assert
        bound.Url.IsAbsoluteUri.Should().BeFalse();
        bound.Url.ToString().Should().Be("/foo");
    }

    [Fact(DisplayName =
        "Isolated [FromBody] SubmitUrlRequest: absolute https url binds successfully, " +
        "because that is the control path Isolated would pass into PostSubmitUrlHandler.")]
    public async Task absolute_https_url_binds()
    {
        // Arrange
        var path = _fixture.CreateGuid().ToString("N");
        var expected = new Uri($"https://example.com/{path}");
        var json = JsonSerializer.Serialize(new { url = expected.ToString() });

        // Act
        var bound = await BindAsync(json);

        // Assert
        bound.Url.Should().Be(expected);
        bound.PodcastId.Should().BeNull();
        bound.PodcastName.Should().BeNull();
    }

    [Fact(DisplayName =
        "Isolated [FromBody] SubmitUrlRequest: ftp url binds as an absolute Uri with ftp scheme, " +
        "because System.Text.Json Uri conversion accepts absolute non-http schemes.")]
    public async Task ftp_url_binds_as_absolute_ftp()
    {
        // Arrange
        const string json = """{"url":"ftp://example.com/episode"}""";

        // Act
        var bound = await BindAsync(json);

        // Assert
        bound.Url.IsAbsoluteUri.Should().BeTrue();
        bound.Url.Scheme.Should().Be(Uri.UriSchemeFtp);
        bound.Url.ToString().Should().Be("ftp://example.com/episode");
    }

    [Fact(DisplayName =
        "Isolated [FromBody] SubmitUrlRequest: file url binds as an absolute Uri with file scheme, " +
        "because System.Text.Json Uri conversion accepts absolute non-http schemes.")]
    public async Task file_url_binds_as_absolute_file()
    {
        // Arrange
        const string json = """{"url":"file:///tmp/episode"}""";

        // Act
        var bound = await BindAsync(json);

        // Assert
        bound.Url.IsAbsoluteUri.Should().BeTrue();
        bound.Url.Scheme.Should().Be(Uri.UriSchemeFile);
        bound.Url.AbsolutePath.Should().Be("/tmp/episode");
    }

    [Fact(DisplayName =
        "Isolated [FromBody] SubmitUrlRequest: uppercase HTTP scheme binds as an absolute Uri with Scheme http, " +
        "because System.Uri normalises the scheme to lowercase.")]
    public async Task uppercase_http_scheme_binds_as_lowercase_http()
    {
        // Arrange
        var path = _fixture.CreateGuid().ToString("N");
        var json = $$"""{"url":"HTTP://example.com/{{path}}"}""";

        // Act
        var bound = await BindAsync(json);

        // Assert
        bound.Url.IsAbsoluteUri.Should().BeTrue();
        bound.Url.Scheme.Should().Be(Uri.UriSchemeHttp);
        bound.Url.AbsolutePath.Should().Be($"/{path}");
    }

    private static async Task<SubmitUrlRequest> BindAsync(string json)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        var bound = await IsolatedSerializer.DeserializeAsync(
            stream, typeof(SubmitUrlRequest), CancellationToken.None);
        return (SubmitUrlRequest)bound!;
    }
}

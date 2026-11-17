using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Auth0.Configuration;
using RedditPodcastPoster.EdgeApi.Configuration;
using RedditPodcastPoster.EntitySearchIndexer.D1Search;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using Xunit;

namespace Indexer.Tests;

public class D1SearchTokenSourceTests
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private readonly RecordingHandler _handler = new();
    private readonly D1SearchOptions _d1;
    private readonly Auth0Options _auth0 = new()
    {
        Domain = "auth.example.test",
        Audience = "https://api.example.test/",
        ClientId = "curator-client-id",
        ClientSecret = "curator-client-secret"
    };

    public D1SearchTokenSourceTests()
    {
        _d1 = new D1SearchOptions
        {
            Enabled = true,
            ClientId = _fixture.CreateTitle(),
            ClientSecret = _fixture.CreateTitle()
        };
        _mocker.Use(new D1SearchConnection(
            _d1,
            new ApiOptions { Endpoint = new Uri("https://api.example.test/") },
            _auth0));
        _mocker.Use(TimeProvider.System);
        _mocker.GetMock<IHttpClientFactory>()
            .Setup(f => f.CreateClient(D1SearchTokenSource.HttpClientName))
            .Returns(() => new HttpClient(_handler, disposeHandler: false) { BaseAddress = new Uri("https://placeholder.test/") });
    }

    [Fact(DisplayName =
        "The D1 writer token is requested from auth0client:Domain with the d1Search client id and audience, not the " +
        "curator auth0client client, because the search-index-writer app is the only credential that may write the index.")]
    public async Task requests_a_token_with_the_writer_client_from_the_shared_auth0_domain()
    {
        // Arrange
        var sut = _mocker.CreateInstance<D1SearchTokenSource>();

        // Act
        var token = await sut.GetToken(CancellationToken.None);

        // Assert
        token.Should().Be("writer-token");
        _handler.Uris.Should().Equal("https://auth.example.test/oauth/token");
        _handler.Bodies.Should().ContainSingle();
        var body = _handler.Bodies[0];
        body.GetProperty("client_id").GetString().Should().Be(_d1.ClientId);
        body.GetProperty("client_secret").GetString().Should().Be(_d1.ClientSecret);
        body.GetProperty("audience").GetString().Should().Be("https://api.example.test/");
        body.GetProperty("grant_type").GetString().Should().Be("client_credentials");
        body.TryGetProperty("client_id", out _).Should().BeTrue();
        body.GetProperty("client_id").GetString().Should().NotBe(_auth0.ClientId);
    }

    /// <summary>Records the token request and answers with a bearer token.</summary>
    private sealed class RecordingHandler : HttpMessageHandler
    {
        public List<string> Uris { get; } = [];
        public List<JsonElement> Bodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Uris.Add(request.RequestUri!.AbsoluteUri);
            using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));
            Bodies.Add(body.RootElement.Clone());
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"access_token":"writer-token","expires_in":86400}""", Encoding.UTF8, "application/json")
            };
        }
    }
}

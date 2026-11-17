using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.EntitySearchIndexer.D1Search;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using Xunit;

namespace Indexer.Tests;

public class D1SearchIndexWriterTests
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private readonly RecordingHandler _handler = new();
    private readonly D1SearchOptions _options = new() { RetryDelayMilliseconds = 0 };

    public D1SearchIndexWriterTests()
    {
        _options.Targets.Add(Target("production"));
        _mocker.Use<IOptions<D1SearchOptions>>(Options.Create(_options));
        _mocker.Use(TimeProvider.System);
        _mocker.GetMock<IHttpClientFactory>()
            .Setup(f => f.CreateClient(D1SearchIndexWriter.HttpClientName))
            .Returns(() => new HttpClient(_handler, disposeHandler: false));
        _mocker.GetMock<ID1SearchTokenSource>()
            .Setup(t => t.GetToken(It.IsAny<D1SearchTarget>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _fixture.CreateTitle());
    }

    [Fact(DisplayName =
        "The writer splits 250 upserts into requests of at most 100 upserts each, because the search Worker refuses a " +
        "larger body, and reports the summed outcome.")]
    public async Task chunks_upserts_into_requests_of_100()
    {
        // Arrange
        var upserts = Enumerable.Range(0, 250).Select(_ => Document()).ToArray();
        var sut = _mocker.CreateInstance<D1SearchIndexWriter>();

        // Act
        var result = await sut.Write(upserts, [], CancellationToken.None);

        // Assert
        _handler.Bodies.Select(b => b.GetProperty("upserts").GetArrayLength()).Should().Equal(100, 100, 50);
        result.Succeeded.Should().BeTrue();
        result.Targets.Single().Upserted.Should().Be(250);
        _handler.Paths.Should().OnlyContain(p => p == "/search/admin/docs");
    }

    [Fact(DisplayName =
        "A target that is switched off, or switched on without credentials, is never called, because the D1 sync must " +
        "stay inert until SP-2.5 has delivered the writer's credentials and Jon has enabled it.")]
    public async Task skips_disabled_and_unconfigured_targets()
    {
        // Arrange
        _options.Targets.Clear();
        _options.Targets.Add(Target("off", t => t.Enabled = false));
        _options.Targets.Add(Target("no-secret", t => t.ClientSecret = string.Empty));
        var sut = _mocker.CreateInstance<D1SearchIndexWriter>();

        // Act
        var result = await sut.Write([Document()], [_fixture.CreateGuid().ToString()], CancellationToken.None);

        // Assert
        sut.IsActive.Should().BeFalse();
        result.Targets.Should().BeEmpty();
        _handler.Bodies.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "A 503 from the search Worker is retried once and the write succeeds, because D1 writes are idempotent and " +
        "the Worker uses 503 for a retryable D1 failure.")]
    public async Task retries_a_503_once()
    {
        // Arrange
        _handler.Statuses.Enqueue(HttpStatusCode.ServiceUnavailable);
        var sut = _mocker.CreateInstance<D1SearchIndexWriter>();

        // Act
        var result = await sut.Write([Document()], [], CancellationToken.None);

        // Assert
        _handler.Bodies.Should().HaveCount(2);
        result.Succeeded.Should().BeTrue();
    }

    [Fact(DisplayName =
        "A Cloudflare challenge page fails the target as a challenge, without a retry, because Bot Fight Mode repeats " +
        "the challenge and the fix is the target's bypass host, not another attempt.")]
    public async Task a_cloudflare_challenge_is_reported_and_not_retried()
    {
        // Arrange
        _handler.HtmlBodies.Enqueue("<!DOCTYPE html><html><title>Just a moment...</title></html>");
        var sut = _mocker.CreateInstance<D1SearchIndexWriter>();

        // Act
        var result = await sut.Write([Document()], [], CancellationToken.None);

        // Assert
        _handler.Bodies.Should().HaveCount(1);
        result.Succeeded.Should().BeFalse();
        result.Targets.Single().Error.Should().Contain("Cloudflare Bot Fight challenge");
    }

    [Fact(DisplayName =
        "A 400 from the search Worker fails the target without throwing and without a retry, because a D1 failure " +
        "must never break Azure indexing and a bad body will not get better.")]
    public async Task a_400_fails_the_target_without_throwing()
    {
        // Arrange
        _handler.Statuses.Enqueue(HttpStatusCode.BadRequest);
        var sut = _mocker.CreateInstance<D1SearchIndexWriter>();

        // Act
        var result = await sut.Write([Document()], [], CancellationToken.None);

        // Assert
        _handler.Bodies.Should().HaveCount(1);
        result.Succeeded.Should().BeFalse();
        result.Targets.Single().Error.Should().Contain("HTTP 400");
    }

    [Fact(DisplayName =
        "A 401 drops the cached Auth0 token, because a rotated secret or rebuilt client must get a fresh token on " +
        "the next write.")]
    public async Task a_401_invalidates_the_cached_token()
    {
        // Arrange
        _handler.Statuses.Enqueue(HttpStatusCode.Unauthorized);
        var sut = _mocker.CreateInstance<D1SearchIndexWriter>();

        // Act
        var result = await sut.Write([Document()], [], CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeFalse();
        _mocker.GetMock<ID1SearchTokenSource>().Verify(t => t.Invalidate(_options.Targets[0]), Times.Once);
    }

    [Fact(DisplayName =
        "A token failure fails the target without throwing, because Auth0 being down must not break Azure indexing.")]
    public async Task a_token_failure_does_not_throw()
    {
        // Arrange
        _mocker.GetMock<ID1SearchTokenSource>()
            .Setup(t => t.GetToken(It.IsAny<D1SearchTarget>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new D1SearchWriteException(_fixture.CreateTitle()));
        var sut = _mocker.CreateInstance<D1SearchIndexWriter>();

        // Act
        var result = await sut.Write([Document()], [], CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeFalse();
        _handler.Bodies.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "Each id is sent once: the last upsert of an id wins and an id both upserted and deleted is upserted, because " +
        "the search Worker rejects a request that names an id twice.")]
    public async Task sends_each_id_once()
    {
        // Arrange
        var id = _fixture.CreateGuid().ToString();
        var first = Document(id);
        var last = Document(id);
        var sut = _mocker.CreateInstance<D1SearchIndexWriter>();

        // Act
        await sut.Write([first, last], [id, id], CancellationToken.None);

        // Assert
        var body = _handler.Bodies.Single();
        body.GetProperty("upserts").EnumerateArray().Select(d => d.GetProperty("title").GetString())
            .Should().Equal(last.Title);
        body.GetProperty("deletes").GetArrayLength().Should().Be(0);
    }

    [Fact(DisplayName =
        "The inventory pages through GET search/admin/docs/inventory, passing next back as after until it is null, " +
        "because the reconcile needs every id in the index and a page holds at most 5000.")]
    public async Task reads_the_whole_inventory_page_by_page()
    {
        // Arrange
        var first = _fixture.CreateGuid().ToString();
        var second = _fixture.CreateGuid().ToString();
        _handler.InventoryPages.Enqueue($"{{\"items\":[{{\"id\":\"{first}\",\"modifiedAt\":5}}],\"next\":\"{first}\"}}");
        _handler.InventoryPages.Enqueue($"{{\"items\":[{{\"id\":\"{second}\",\"modifiedAt\":7}}],\"next\":null}}");
        var sut = _mocker.CreateInstance<D1SearchIndexWriter>();

        // Act
        var inventory = await sut.ReadInventory("production", CancellationToken.None);

        // Assert
        inventory.Should().BeEquivalentTo(new Dictionary<string, long> { [first] = 5, [second] = 7 });
        _handler.Paths.Should().OnlyContain(p => p == "/search/admin/docs/inventory");
        _handler.Queries.Should().Equal("?limit=5000", $"?limit=5000&after={first}");
    }

    [Fact(DisplayName =
        "A write to one named target reaches only that target, because the reconcile patches each index against its " +
        "own inventory and must not write the other.")]
    public async Task a_named_write_reaches_only_that_target()
    {
        // Arrange
        _options.Targets.Add(Target("preview", t => t.Endpoint = new Uri("https://preview.example.test/")));
        var sut = _mocker.CreateInstance<D1SearchIndexWriter>();

        // Act
        var result = await sut.Write("preview", [Document()], [], CancellationToken.None);

        // Assert
        result.Targets.Select(t => t.Target).Should().Equal("preview");
        _handler.Bodies.Should().HaveCount(1);
        sut.UsableTargets.Should().Equal("production", "preview");
    }

    [Fact(DisplayName =
        "Asking for the inventory of a target that is not configured throws, because a reconcile without the whole " +
        "inventory would treat live documents as orphans.")]
    public async Task inventory_of_an_unconfigured_target_throws()
    {
        // Arrange
        var sut = _mocker.CreateInstance<D1SearchIndexWriter>();

        // Act
        var act = () => sut.ReadInventory("preview", CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<D1SearchWriteException>();
    }

    private D1SearchTarget Target(string name, Action<D1SearchTarget>? customise = null)
    {
        var target = new D1SearchTarget
        {
            Name = name,
            Enabled = true,
            Endpoint = new Uri("https://api.example.test/"),
            Auth0Domain = "auth.example.test",
            Audience = "https://api.example.test/",
            ClientId = _fixture.CreateTitle(),
            ClientSecret = _fixture.CreateTitle()
        };
        customise?.Invoke(target);
        return target;
    }

    private D1SearchDocument Document(string? id = null) => new()
    {
        Id = id ?? _fixture.CreateGuid().ToString(),
        Title = _fixture.CreateTitle(),
        Release = DateTimeOffset.UtcNow,
        Duration = "00:10:00",
        ModifiedAt = 1
    };

    /// <summary>Records each request body and answers with queued statuses, then 200 with an outcome.</summary>
    private sealed class RecordingHandler : HttpMessageHandler
    {
        public Queue<HttpStatusCode> Statuses { get; } = new();
        public Queue<string> HtmlBodies { get; } = new();
        public Queue<string> InventoryPages { get; } = new();
        public List<string> Queries { get; } = [];
        public List<JsonElement> Bodies { get; } = [];
        public List<string> Paths { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            if (request.Method == HttpMethod.Get)
            {
                Paths.Add(request.RequestUri!.AbsolutePath);
                Queries.Add(request.RequestUri.Query);
                return new HttpResponseMessage(HttpStatusCode.OK)
                    { Content = new StringContent(InventoryPages.Dequeue(), Encoding.UTF8, "application/json") };
            }

            using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));
            Bodies.Add(body.RootElement.Clone());
            Paths.Add(request.RequestUri!.AbsolutePath);
            if (HtmlBodies.TryDequeue(out var html))
            {
                return new HttpResponseMessage(HttpStatusCode.Forbidden)
                    { Content = new StringContent(html, Encoding.UTF8, "text/html") };
            }

            if (Statuses.TryDequeue(out var status))
            {
                return new HttpResponseMessage(status)
                    { Content = new StringContent("{\"error\":{\"code\":\"\",\"message\":\"x\"}}", Encoding.UTF8, "application/json") };
            }

            var upserts = body.RootElement.GetProperty("upserts").GetArrayLength();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    $"{{\"upserted\":{upserts},\"deleted\":0,\"stale\":[],\"notFound\":[]}}", Encoding.UTF8, "application/json")
            };
        }
    }
}

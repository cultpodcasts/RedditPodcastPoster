using FluentAssertions;
using Moq;

namespace RedditPodcastPoster.DependencyInjection.Tests;

public class AsyncInstanceTests
{
    [Fact(DisplayName =
        "AsyncInstance GetAsync: when factory succeeds, then result is cached, because subsequent callers must not re-create.")]
    public async Task GetAsync_caches_successful_result()
    {
        // Arrange
        var factory = new Mock<IAsyncFactory<string>>();
        factory.Setup(x => x.Create(It.IsAny<CancellationToken>())).ReturnsAsync("warmed");
        var sut = new AsyncInstance<string>(factory.Object);

        // Act
        var first = await sut.GetAsync();
        var second = await sut.GetAsync();

        // Assert
        first.Should().Be("warmed");
        second.Should().Be("warmed");
        factory.Verify(x => x.Create(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName =
        "AsyncInstance GetAsync: when factory is cancelled, then next GetAsync retries Create, because cancelled init must not poison the cache.")]
    public async Task GetAsync_retries_after_cancellation()
    {
        // Arrange
        var attempt = 0;
        var factory = new Mock<IAsyncFactory<string>>();
        factory.Setup(x => x.Create(It.IsAny<CancellationToken>()))
            .Returns<CancellationToken>(async ct =>
            {
                attempt++;
                if (attempt == 1)
                {
                    await Task.Delay(Timeout.InfiniteTimeSpan, ct);
                }

                return "recovered";
            });
        var sut = new AsyncInstance<string>(factory.Object);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // Act
        var cancelled = async () => await sut.GetAsync(cts.Token);
        await cancelled.Should().ThrowAsync<OperationCanceledException>();
        var recovered = await sut.GetAsync();

        // Assert
        recovered.Should().Be("recovered");
        attempt.Should().Be(2);
    }

    [Fact(DisplayName =
        "AsyncInstance GetAsync: when WarmAsync-style caller passes a token, then Create receives that token, because host shutdown must cancel factory I/O.")]
    public async Task GetAsync_forwards_cancellation_token_to_Create()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        CancellationToken? seen = null;
        var factory = new Mock<IAsyncFactory<int>>();
        factory.Setup(x => x.Create(It.IsAny<CancellationToken>()))
            .Returns<CancellationToken>(ct =>
            {
                seen = ct;
                return Task.FromResult(1);
            });
        var sut = new AsyncInstance<int>(factory.Object);

        // Act
        await sut.GetAsync(cts.Token);

        // Assert
        seen.Should().Be(cts.Token);
    }
}

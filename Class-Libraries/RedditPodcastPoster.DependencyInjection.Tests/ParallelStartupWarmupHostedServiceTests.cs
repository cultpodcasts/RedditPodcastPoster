using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace RedditPodcastPoster.DependencyInjection.Tests;

public class ParallelStartupWarmupHostedServiceTests
{
    [Fact(DisplayName =
        "Startup warm failure: when one warmer throws, then that warmer is named in LogError and AggregateException, because host-start diagnosis must identify the warm.")]
    public async Task StartAsync_when_one_warmer_fails_logs_warmer_name_and_throws()
    {
        // Arrange
        var boom = new InvalidOperationException("auth0 config missing");
        var warmers = new IStartupWarmer[]
        {
            new StubWarmer("ok-warmer"),
            new StubWarmer("auth0-signing-keys", boom)
        };
        var logger = new Mock<ILogger<ParallelStartupWarmupHostedService>>();
        var sut = new ParallelStartupWarmupHostedService(warmers, logger.Object);

        // Act
        var act = async () => await sut.StartAsync(CancellationToken.None);

        // Assert
        var thrown = await act.Should().ThrowAsync<AggregateException>();
        thrown.Which.InnerExceptions.Should().ContainSingle()
            .Which.Should().BeSameAs(boom);
        thrown.Which.Message.Should().Contain("auth0-signing-keys");

        VerifyErrorLoggedContaining(logger, "Startup warm failed for");
        VerifyErrorLoggedContaining(logger, "StartupWarmFailed");
    }

    [Fact(DisplayName =
        "Startup warm failure: when multiple warmers throw, then each is LogError'd and AggregateException lists all names, because parallel warm must not hide secondary failures.")]
    public async Task StartAsync_when_multiple_warmers_fail_logs_each_and_summarises()
    {
        // Arrange
        var auth0Error = new InvalidOperationException("jwks unreachable");
        var appleError = new HttpRequestException("apple token endpoint 503");
        var warmers = new IStartupWarmer[]
        {
            new StubWarmer("title-casing"),
            new StubWarmer("auth0-signing-keys", auth0Error),
            new StubWarmer("apple-http-client", appleError)
        };
        var logger = new Mock<ILogger<ParallelStartupWarmupHostedService>>();
        var sut = new ParallelStartupWarmupHostedService(warmers, logger.Object);

        // Act
        var act = async () => await sut.StartAsync(CancellationToken.None);

        // Assert
        var thrown = await act.Should().ThrowAsync<AggregateException>();
        thrown.Which.InnerExceptions.Should().HaveCount(2);
        thrown.Which.InnerExceptions.Should().Contain(auth0Error);
        thrown.Which.InnerExceptions.Should().Contain(appleError);
        thrown.Which.Message.Should().Contain("auth0-signing-keys");
        thrown.Which.Message.Should().Contain("apple-http-client");

        VerifyErrorLoggedContaining(logger, "auth0-signing-keys");
        VerifyErrorLoggedContaining(logger, "apple-http-client");
        VerifyErrorLoggedContaining(logger, "StartupWarmFailed");
        VerifyErrorLoggedContaining(logger, "count='2'");
    }

    private static void VerifyErrorLoggedContaining(
        Mock<ILogger<ParallelStartupWarmupHostedService>> logger,
        string fragment)
    {
        logger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((state, _) => state.ToString()!.Contains(fragment, StringComparison.Ordinal)),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    private sealed class StubWarmer(string name, Exception? failWith = null) : IStartupWarmer
    {
        public string Name => name;

        public Task WarmAsync(CancellationToken cancellationToken) =>
            failWith is null ? Task.CompletedTask : Task.FromException(failWith);
    }
}

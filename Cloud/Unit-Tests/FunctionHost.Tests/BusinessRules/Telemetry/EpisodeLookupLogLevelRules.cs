using System.Text.Json;
using Azure;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.EpisodeLookup;
using Xunit;

namespace FunctionHost.Tests.BusinessRules.Telemetry;

public class EpisodeLookupLogLevelRules
{
    private static ILoggerFactory CreateLoggerFactory()
    {
        var services = new ServiceCollection();
        services.AddLogging(logging =>
        {
            logging.SetMinimumLevel(LogLevel.Trace);
            logging.AddProvider(new NullSinkLoggerProvider());
            ApplicationLogLevels.Configure(logging);
        });
        return services.BuildServiceProvider().GetRequiredService<ILoggerFactory>();
    }

    [Theory(DisplayName =
        "Function apps keep the episode lookup's Information lines (the per-lookup outcome and guard timeouts), " +
        "because they are the only record of which platform, flow, plan and stage served a lookup.")]
    [InlineData(typeof(CatalogueLookupExecutor))]
    [InlineData(typeof(CatalogueRequestGuard))]
    public void episode_lookup_categories_log_at_information(Type loggerType)
    {
        // Arrange
        using var factory = CreateLoggerFactory();

        // Act
        var logger = factory.CreateLogger(loggerType.FullName!);

        // Assert
        loggerType.FullName.Should().StartWith(ApplicationLogLevels.EpisodeLookupCategory + ".");
        logger.IsEnabled(LogLevel.Information).Should().BeTrue();
        logger.IsEnabled(LogLevel.Debug).Should().BeFalse();
    }

    [Fact(DisplayName =
        "Other RedditPodcastPoster categories stay at Warning in function apps, because their pagination logs are high-volume.")]
    public void other_reddit_podcast_poster_categories_stay_at_warning()
    {
        // Arrange
        using var factory = CreateLoggerFactory();

        // Act
        var logger = factory.CreateLogger("RedditPodcastPoster.PodcastServices.Spotify.Providers.SpotifyShowEpisodePages");

        // Assert
        logger.IsEnabled(LogLevel.Information).Should().BeFalse();
        logger.IsEnabled(LogLevel.Warning).Should().BeTrue();
    }

    [Theory(DisplayName =
        "Every function app that registers episode lookup sets the EpisodeLookup category to Information in host.json, " +
        "matching the worker filter, while RedditPodcastPoster stays at Warning.")]
    [InlineData("Indexer")]
    [InlineData("Discovery")]
    [InlineData("Api")]
    public void host_json_keeps_episode_lookup_at_information(string functionApp)
    {
        // Arrange
        var hostJson = Path.Combine(FindRepoRoot(), "Cloud", functionApp, "host.json");

        // Act
        using var document = JsonDocument.Parse(File.ReadAllText(hostJson));
        var logLevel = document.RootElement.GetProperty("logging").GetProperty("logLevel");

        // Assert
        logLevel.GetProperty(ApplicationLogLevels.EpisodeLookupCategory).GetString().Should().Be("Information");
        logLevel.GetProperty("RedditPodcastPoster").GetString().Should().Be("Warning");
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "RedditPodcastPoster.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("RedditPodcastPoster.slnx not found above the test output.");
    }

    /// <summary>A provider is needed for filter rules to apply; it records nothing.</summary>
    private sealed class NullSinkLoggerProvider : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName) => new NullSinkLogger();

        void IDisposable.Dispose()
        {
        }

        private sealed class NullSinkLogger : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            void ILogger.Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
            }
        }
    }
}

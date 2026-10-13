using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.Episodes.Extensions;
using RedditPodcastPoster.Episodes.Matching.Candidates;

namespace RedditPodcastPoster.Episodes.Tests.BusinessRules.Matching.Candidates;

/// <summary>
/// catalogueCandidatePolicy options are validated at startup (ValidateOnStart), so a bad config fails fast
/// instead of on the first Spotify show-episodes call (which rejects a limit above 50).
/// </summary>
public class CatalogueCandidatePolicyOptionsRules
{
    [Theory(DisplayName =
        "Startup validation rejects RecentShowEpisodesPageSize outside 1..50 or a RecentWindow that is not greater than zero.")]
    [InlineData("0", "14.00:00:00")]
    [InlineData("51", "14.00:00:00")]
    [InlineData("50", "00:00:00")]
    [InlineData("10", "-1.00:00:00")]
    public void Invalid_options_fail_at_startup(string pageSize, string recentWindow)
    {
        // Arrange
        var provider = Build(pageSize, recentWindow);

        // Act
        var act = () => provider.GetRequiredService<IStartupValidator>().Validate();

        // Assert
        act.Should().Throw<OptionsValidationException>()
            .WithMessage($"*{CatalogueCandidatePolicyOptions.ValidationMessage}*");
    }

    [Theory(DisplayName =
        "Startup validation accepts the defaults and any page size in 1..50 with a positive RecentWindow.")]
    [InlineData(null, null)]
    [InlineData("1", "1.00:00:00")]
    [InlineData("50", "14.00:00:00")]
    public void Valid_options_pass_startup_validation(string? pageSize, string? recentWindow)
    {
        // Arrange
        var provider = Build(pageSize, recentWindow);

        // Act
        var act = () => provider.GetRequiredService<IStartupValidator>().Validate();

        // Assert
        act.Should().NotThrow();
    }

    private static ServiceProvider Build(string? pageSize, string? recentWindow)
    {
        var values = new Dictionary<string, string?>();
        if (pageSize != null)
        {
            values[$"{CatalogueCandidatePolicyOptions.SectionName}:RecentShowEpisodesPageSize"] = pageSize;
        }

        if (recentWindow != null)
        {
            values[$"{CatalogueCandidatePolicyOptions.SectionName}:RecentWindow"] = recentWindow;
        }

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(values).Build());
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddEpisodesDomain();
        return services.BuildServiceProvider();
    }
}

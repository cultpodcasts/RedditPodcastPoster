using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.EpisodeLookup.Extensions;
using RedditPodcastPoster.EpisodeLookup;
using RedditPodcastPoster.EpisodeLookup;
using RedditPodcastPoster.Episodes.Matching;
using RedditPodcastPoster.Episodes.Matching.Candidates;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;

namespace RedditPodcastPoster.EpisodeLookup.Tests.Unit;

/// <summary>Planner tunables are validated at startup, so a bad configuration fails fast instead of at the first lookup.</summary>
public class EpisodeLookupOptionsTests
{
    [Fact(DisplayName = "Defaults are valid: full read <=200 episodes, 14-day recent window, Index cap 20 pages, walks Spotify 4 / Apple 5 pages, 10 s timeout, 20 s non-Index full-read budget.")]
    public void Defaults_are_valid()
    {
        // Arrange
        var options = new EpisodeLookupOptions();

        // Assert
        EpisodeLookupOptions.IsValid(options).Should().BeTrue();
        options.FullShowReadMaxEpisodes.Should().Be(200);
        options.RecentWindow.Should().Be(TimeSpan.FromDays(14));
        options.IndexMaxPages.Should().Be(20);
        options.SpotifyBoundedWalkPages.Should().Be(4);
        options.AppleBoundedWalkPages.Should().Be(5);
        options.RequestTimeout.Should().Be(TimeSpan.FromSeconds(10));
        options.FullShowReadBudget.Should().Be(TimeSpan.FromSeconds(20));
    }

    [Theory(DisplayName = "Out-of-range values are invalid.")]
    [InlineData(-1, 14, 20)]
    [InlineData(1001, 14, 20)]
    [InlineData(200, 0, 20)]
    [InlineData(200, 14, 0)]
    [InlineData(200, 14, 101)]
    [InlineData(200, 14, 20, 0)]
    [InlineData(200, 14, 20, 4, 21)]
    [InlineData(200, 14, 20, 4, 5, 0)]
    [InlineData(200, 14, 20, 4, 5, 121)]
    [InlineData(200, 14, 20, 4, 5, 10, 0)]
    [InlineData(200, 14, 20, 4, 5, 10, 301)]
    public void Out_of_range_is_invalid(int fullRead, int recentDays, int indexPages, int spotifyWalk = 4, int appleWalk = 5, int timeoutSeconds = 10, int budgetSeconds = 20)
    {
        // Arrange
        var options = new EpisodeLookupOptions
        {
            FullShowReadMaxEpisodes = fullRead,
            RecentWindow = TimeSpan.FromDays(recentDays),
            IndexMaxPages = indexPages,
            SpotifyBoundedWalkPages = spotifyWalk,
            AppleBoundedWalkPages = appleWalk,
            RequestTimeout = TimeSpan.FromSeconds(timeoutSeconds),
            FullShowReadBudget = TimeSpan.FromSeconds(budgetSeconds)
        };

        // Assert
        EpisodeLookupOptions.IsValid(options).Should().BeFalse();
    }

    [Fact(DisplayName = "An invalid episodeLookup section fails options resolution with the validation message.")]
    public void Invalid_configuration_fails_resolution()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{EpisodeLookupOptions.SectionName}:{nameof(EpisodeLookupOptions.IndexMaxPages)}"] = "0"
            })
            .Build();
        var services = new ServiceCollection().AddSingleton<IConfiguration>(configuration);
        services.AddLogging();
        services.AddEpisodeLookup();
        using var provider = services.BuildServiceProvider();

        // Act
        var act = () => provider.GetRequiredService<IOptions<EpisodeLookupOptions>>().Value;

        // Assert
        act.Should().Throw<OptionsValidationException>().WithMessage($"*{EpisodeLookupOptions.ValidationMessage}*");
    }
}

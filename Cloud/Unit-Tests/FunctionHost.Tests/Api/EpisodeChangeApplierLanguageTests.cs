using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Api.Models;
using Api.Services.Episodes;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using Xunit;
using Episode = RedditPodcastPoster.Models.Episodes.Episode;

namespace FunctionHost.Tests.Api;

public class EpisodeChangeApplierLanguageTests
{
    private static EpisodeChangeApplier CreateSut() =>
        new(NullLogger<EpisodeChangeApplier>.Instance);

    private static Episode CreateEpisode(Action<Episode>? customize = null)
    {
        var episode = new Episode
        {
            Id = Guid.NewGuid(),
            PodcastId = Guid.NewGuid(),
            Title = "Original title",
            Description = "Original description",
            Release = DateTime.UtcNow.AddDays(-30),
            Length = TimeSpan.FromMinutes(30),
            Urls = new ServiceUrls(),
            Language = "fil"
        };
        customize?.Invoke(episode);
        return episode;
    }

    [Fact(DisplayName =
        "Apply clears Language when the request sets an empty string, because empty means English/default")]
    public void Apply_clears_language_on_empty_string()
    {
        // Arrange
        var episode = CreateEpisode();
        var sut = CreateSut();

        // Act
        sut.Apply(episode, new EpisodeChangeRequest { Language = "" });

        // Assert
        episode.Language.Should().BeNull();
    }

    [Fact(DisplayName =
        "Apply stores English language codes as null because English is the product default")]
    public void Apply_clears_language_when_english_code_is_set()
    {
        // Arrange
        var episode = CreateEpisode();
        var sut = CreateSut();

        // Act
        sut.Apply(episode, new EpisodeChangeRequest { Language = "en" });

        // Assert
        episode.Language.Should().BeNull();
    }

    [Fact(DisplayName =
        "Apply stores regional English codes as null (en-GB) so search treats them as English")]
    public void Apply_clears_language_when_regional_english_code_is_set()
    {
        // Arrange
        var episode = CreateEpisode(e => e.Language = "es");
        var sut = CreateSut();

        // Act
        sut.Apply(episode, new EpisodeChangeRequest { Language = "en-GB" });

        // Assert
        episode.Language.Should().BeNull();
    }

    [Fact(DisplayName = "Apply keeps non-English language codes on the episode")]
    public void Apply_sets_non_english_language_code()
    {
        // Arrange
        var episode = CreateEpisode(e => e.Language = null);
        var sut = CreateSut();

        // Act
        sut.Apply(episode, new EpisodeChangeRequest { Language = "fil" });

        // Assert
        episode.Language.Should().Be("fil");
    }
}

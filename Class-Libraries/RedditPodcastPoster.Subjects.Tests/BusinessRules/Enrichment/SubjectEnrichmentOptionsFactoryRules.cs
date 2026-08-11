using FluentAssertions;
using Moq;
using RedditPodcastPoster.DependencyInjection;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Subjects.Factories;
using RedditPodcastPoster.Text.TitleCasing;

namespace RedditPodcastPoster.Subjects.Tests.BusinessRules.Enrichment;

/// <summary>
/// HARD integrity rules for subject-enrichment language. See docs/episode-language.md.
/// </summary>
public class SubjectEnrichmentOptionsFactoryRules
{
    private readonly DomainTestFixture _fixture = new();

    [Fact(DisplayName =
        "Subject enrichment ignores: when podcast and language lists both have names, then the union is case-insensitive and duplicates collapse, because language rules and podcast overrides combine.")]
    public void union_ignore_lists_is_case_insensitive()
    {
        // Arrange
        var podcast = new[] { "House Of Yahweh", "Other" };
        var language = new[] { "house of yahweh", "Hoy Alias" };

        // Act
        var merged = SubjectEnrichmentOptionsFactory.UnionIgnoreLists(podcast, language);

        // Assert
        merged.Should().NotBeNull();
        merged!.Should().HaveCount(3);
        merged.Should().Contain(x => x.Equals("House Of Yahweh", StringComparison.OrdinalIgnoreCase));
        merged.Should().Contain(x => x.Equals("Other", StringComparison.OrdinalIgnoreCase));
        merged.Should().Contain(x => x.Equals("Hoy Alias", StringComparison.OrdinalIgnoreCase));
    }

    [Fact(DisplayName =
        "Subject enrichment ignores: when both ignore lists are null or empty, then the union is null, because enrichment skips ignore filtering when there is nothing to ignore.")]
    public void union_ignore_lists_null_when_empty()
    {
        // Arrange
        // Act
        var merged = SubjectEnrichmentOptionsFactory.UnionIgnoreLists(null, Array.Empty<string>());

        // Assert
        merged.Should().BeNull();
    }

    [Fact(DisplayName =
        "Subject enrichment ignores: when only the language list has names, then those names are returned, because non-English language TitleCasingRules can ignore subjects without a podcast override.")]
    public void union_ignore_lists_language_only()
    {
        // Arrange
        var language = new[] { "Alpha", "Beta" };

        // Act
        var merged = SubjectEnrichmentOptionsFactory.UnionIgnoreLists(null, language);

        // Assert
        merged.Should().BeEquivalentTo(language, o => o.WithoutStrictOrdering());
    }

    [Fact(DisplayName =
        "Subject enrichment options: when the episode has an explicit language, then ignored subjects come from that language document, because episode language is authoritative at read time.")]
    public async Task create_async_prefers_episode_language()
    {
        // Arrange
        var podcast = _fixture.CreateSpotifyPrimaryPodcast(_fixture.CreateSpotifyId());
        podcast.Language = "fil";
        podcast.IgnoredSubjects = null;
        var episode = _fixture.CreateSpotifyCatalogueEpisode();
        episode.Language = "es";

        var provider = new Mock<ITitleCasingRulesProvider>();
        provider
            .Setup(x => x.GetIgnoredSubjectsAsync("es", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { "Hoy" });
        provider
            .Setup(x => x.GetIgnoredSubjectsAsync("fil", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { "MustNotApply" });
        var instance = new Mock<IAsyncInstance<ITitleCasingRulesProvider>>();
        instance.Setup(x => x.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync(provider.Object);
        var sut = new SubjectEnrichmentOptionsFactory(instance.Object);

        // Act
        var options = await sut.CreateAsync(podcast, episode);

        // Assert
        options.IgnoredSubjects.Should().Equal("Hoy");
        provider.Verify(x => x.GetIgnoredSubjectsAsync("es", It.IsAny<CancellationToken>()), Times.Once);
        provider.Verify(x => x.GetIgnoredSubjectsAsync("fil", It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName =
        "INTEGRITY: when the episode Language is null (product English) on a non-English podcast, CreateAsync must not load that podcast language's ignored subjects, because null means English and coalescing to podcast.Language would corrupt enrichment vs English subject search.")]
    public async Task create_async_null_episode_language_is_english_not_podcast_language()
    {
        // Arrange
        var podcast = _fixture.CreateSpotifyPrimaryPodcast(_fixture.CreateSpotifyId());
        podcast.Language = "fil";
        podcast.IgnoredSubjects = ["LocalOverride"];
        var episode = _fixture.CreateSpotifyCatalogueEpisode();
        episode.Language = null;

        var provider = new Mock<ITitleCasingRulesProvider>();
        provider
            .Setup(x => x.GetIgnoredSubjectsAsync(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<string>());
        provider
            .Setup(x => x.GetIgnoredSubjectsAsync("fil", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { "FilipinoIgnore" });
        var instance = new Mock<IAsyncInstance<ITitleCasingRulesProvider>>();
        instance.Setup(x => x.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync(provider.Object);
        var sut = new SubjectEnrichmentOptionsFactory(instance.Object);

        // Act
        var options = await sut.CreateAsync(podcast, episode);

        // Assert
        options.IgnoredSubjects.Should().Equal("LocalOverride");
        provider.Verify(x => x.GetIgnoredSubjectsAsync(null, It.IsAny<CancellationToken>()), Times.Once);
        provider.Verify(x => x.GetIgnoredSubjectsAsync("fil", It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName =
        "Subject enrichment options: when no episode is supplied, ignored subjects come from the podcast language, because podcast-only paths have no episode language.")]
    public async Task create_async_without_episode_uses_podcast_language()
    {
        // Arrange
        var podcast = _fixture.CreateSpotifyPrimaryPodcast(_fixture.CreateSpotifyId());
        podcast.Language = "fr";
        podcast.IgnoredSubjects = null;

        var provider = new Mock<ITitleCasingRulesProvider>();
        provider
            .Setup(x => x.GetIgnoredSubjectsAsync("fr", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { "Lang" });
        var instance = new Mock<IAsyncInstance<ITitleCasingRulesProvider>>();
        instance.Setup(x => x.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync(provider.Object);
        var sut = new SubjectEnrichmentOptionsFactory(instance.Object);

        // Act
        var options = await sut.CreateAsync(podcast, episode: null);

        // Assert
        options.IgnoredSubjects.Should().Equal("Lang");
        provider.Verify(x => x.GetIgnoredSubjectsAsync("fr", It.IsAny<CancellationToken>()), Times.Once);
    }
}

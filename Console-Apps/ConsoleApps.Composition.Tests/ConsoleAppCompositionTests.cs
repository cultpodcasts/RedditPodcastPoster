using FluentAssertions;
using Index;
using Poster;
using AddAudioPodcast;
using EnrichExistingEpisodesFromPodcastServices;
using SubmitUrl;
using Xunit;

namespace ConsoleApps.Composition.Tests;

public class ConsoleAppCompositionTests
{
    [Fact(DisplayName =
        "Index console app: its service registration resolves IndexProcessor, because a missing registration " +
        "(e.g. IBBCPageMetaDataExtractor needed by BbcNonPodcastServiceAdapter) crashes Index.exe at startup before it indexes anything.")]
    public async Task index_console_services_resolve_index_processor()
    {
        // Arrange
        var services = ConsoleAppCompositionSupport.CreateServiceCollection(s => s.AddIndexConsoleServices());

        // Act
        var act = async () => await ConsoleAppCompositionSupport.ResolveEntryPointAsync(services, typeof(IndexProcessor));

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact(DisplayName =
        "Poster console app: its service registration resolves PostProcessor, because any missing registration " +
        "would crash Poster.exe at startup before it posts anything.")]
    public async Task poster_console_services_resolve_post_processor()
    {
        // Arrange
        var services = ConsoleAppCompositionSupport.CreateServiceCollection(s => s.AddPosterConsoleServices());

        // Act
        var act = async () => await ConsoleAppCompositionSupport.ResolveEntryPointAsync(services, typeof(PostProcessor));

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact(DisplayName =
        "AddAudioPodcast console app: its service registration resolves AddAudioPodcastProcessor, because any missing " +
        "registration would crash AddAudioPodcast.exe at startup before it adds anything.")]
    public async Task add_audio_podcast_console_services_resolve_processor()
    {
        // Arrange
        var services = ConsoleAppCompositionSupport.CreateServiceCollection(s => s.AddAddAudioPodcastConsoleServices());

        // Act
        var act = async () => await ConsoleAppCompositionSupport.ResolveEntryPointAsync(services, typeof(AddAudioPodcastProcessor));

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Theory(DisplayName =
        "SubmitUrl console app: its service registration resolves SubmitUrlProcessor for both enrichers, because a missing " +
        "registration (for example AddCatalogueServices) would crash SubmitUrl.exe at startup before it submits a URL.")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task submit_url_console_services_resolve_processor(bool useRefreshMetaEnricher)
    {
        // Arrange
        var services = ConsoleAppCompositionSupport.CreateServiceCollection(
            s => s.AddSubmitUrlConsoleServices(useRefreshMetaEnricher));

        // Act
        var act = async () => await ConsoleAppCompositionSupport.ResolveEntryPointAsync(services, typeof(SubmitUrlProcessor));

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact(DisplayName =
        "EnrichExistingEpisodesFromPodcastServices console app: its service registration resolves EnrichPodcastEpisodesProcessor, " +
        "because a missing registration would crash the CLI at startup before it enriches anything.")]
    public async Task enrich_existing_episodes_console_services_resolve_processor()
    {
        // Arrange
        var services = ConsoleAppCompositionSupport.CreateServiceCollection(
            s => s.AddEnrichExistingEpisodesConsoleServices());

        // Act
        var act = async () =>
            await ConsoleAppCompositionSupport.ResolveEntryPointAsync(services, typeof(EnrichPodcastEpisodesProcessor));

        // Assert
        await act.Should().NotThrowAsync();
    }
}

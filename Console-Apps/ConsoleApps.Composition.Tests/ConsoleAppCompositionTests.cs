using FluentAssertions;
using Index;
using Poster;
using AddAudioPodcast;
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
}

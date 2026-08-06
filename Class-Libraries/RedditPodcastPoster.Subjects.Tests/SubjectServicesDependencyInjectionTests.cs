using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using RedditPodcastPoster.DependencyInjection;
using RedditPodcastPoster.Persistence.Abstractions.Providers;
using RedditPodcastPoster.Subjects.Extensions;
using RedditPodcastPoster.Subjects.Matching;
using RedditPodcastPoster.Text.Sanitisers;
using RedditPodcastPoster.Text.TitleCasing;

namespace RedditPodcastPoster.Subjects.Tests;

public class SubjectServicesDependencyInjectionTests
{
    [Fact(DisplayName =
        "AddSubjectServices self-containment: when host stubs Cosmos subjects provider and title-casing rules, then ISubjectMatcher resolves, because SubjectService needs ITextSanitiser + ISubjectsProvider.")]
    public void AddSubjectServices_resolves_ISubjectMatcher_with_host_stubs()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSubjectServices();
        ReplaceSubjectsProviderWithStub(services);
        services.AddSingleton(Mock.Of<IAsyncInstance<ITitleCasingRulesProvider>>());

        using var provider = services.BuildServiceProvider();

        // Act
        var matcher = provider.GetRequiredService<ISubjectMatcher>();
        var textSanitiser = provider.GetRequiredService<ITextSanitiser>();

        // Assert
        matcher.Should().NotBeNull();
        textSanitiser.Should().NotBeNull();
    }

    [Fact(DisplayName =
        "AddSubjectServices registration: when called alone, then ITextSanitiser and ISubjectsProvider are registered, because Spotify/Apple hosts must not omit those deps.")]
    public void AddSubjectServices_registers_text_sanitiser_and_subjects_provider()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddSubjectServices();

        // Assert
        services.Should().Contain(d => d.ServiceType == typeof(ITextSanitiser));
        services.Should().Contain(d => d.ServiceType == typeof(ISubjectsProvider));
        services.Should().Contain(d => d.ServiceType == typeof(ISubjectMatcher));
    }

    private static void ReplaceSubjectsProviderWithStub(IServiceCollection services)
    {
        foreach (var descriptor in services.Where(d => d.ServiceType == typeof(ISubjectsProvider)).ToList())
        {
            services.Remove(descriptor);
        }

        services.AddSingleton(Mock.Of<ISubjectsProvider>());
    }
}

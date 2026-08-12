using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using RedditPodcastPoster.Catalogue.Episodes;
using RedditPodcastPoster.Catalogue.Extensions;
using RedditPodcastPoster.Catalogue.Podcasts;
using RedditPodcastPoster.Configuration.Options;
using RedditPodcastPoster.Models.Subjects;
using RedditPodcastPoster.Persistence.Abstractions.Providers;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.PodcastServices.Abstractions.Handlers;
using RedditPodcastPoster.SocialPosting.Episodes;
using RedditPodcastPoster.SocialPosting.Extensions;
using RedditPodcastPoster.SocialPosting.Factories;

namespace RedditPodcastPoster.PodcastServices.Tests.DependencyInjection;

public class CatalogueSocialPostingDependencyInjectionTests
{
    [Fact(DisplayName =
        "Index-like catalogue container: when AddCatalogueServices and AddSocialPostingServices are registered without Reddit, then catalogue and candidacy types are registered, because Index must not require Reddit.NET.")]
    public void index_like_container_registers_catalogue_and_social_without_reddit()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddLogging();
        services.AddCatalogueServices();
        services.AddSocialPostingServices();

        // Assert
        services.Should().Contain(d => d.ServiceType == typeof(IEpisodeProvider));
        services.Should().Contain(d => d.ServiceType == typeof(IFoundEpisodeFilter));
        services.Should().Contain(d => d.ServiceType == typeof(IPodcastFilter));
        services.Should().Contain(d => d.ServiceType == typeof(IPodcastFactory));
        services.Should().Contain(d => d.ServiceType == typeof(IPodcastEpisodeFilter));
        services.Should().Contain(d => d.ServiceType == typeof(IEpisodeProcessor));
        services.Should().Contain(d => d.ServiceType == typeof(IPodcastEpisodePoster));
    }

    [Fact(DisplayName =
        "Index-like catalogue container: when host stubs platform retrieval handlers and repositories, then IEpisodeProvider and IPodcastEpisodeFilter resolve, because indexing needs catalogue intake and candidacy filters without Reddit.")]
    public void index_like_container_resolves_episode_provider_and_podcast_episode_filter()
    {
        // Arrange
        var services = CreateIndexLikeContainer();
        StubCatalogueAndSocialDependencies(services);
        using var provider = services.BuildServiceProvider();

        // Act
        var episodeProvider = provider.GetRequiredService<IEpisodeProvider>();
        var podcastEpisodeFilter = provider.GetRequiredService<IPodcastEpisodeFilter>();
        var podcastFilter = provider.GetRequiredService<IPodcastFilter>();

        // Assert
        episodeProvider.Should().NotBeNull();
        podcastEpisodeFilter.Should().NotBeNull();
        podcastFilter.Should().NotBeNull();
    }

    [Fact(DisplayName =
        "Poster-like container: when Catalogue and SocialPosting are registered, then posting orchestrators resolve without a live Reddit poster port, because Reddit.NET posting is detached.")]
    public void poster_like_container_resolves_posting_orchestrators_without_live_reddit_poster()
    {
        // Arrange
        var services = CreateIndexLikeContainer();
        StubCatalogueAndSocialDependencies(services);
        using var provider = services.BuildServiceProvider();

        // Act
        var episodeProcessor = provider.GetRequiredService<IEpisodeProcessor>();
        var podcastEpisodePoster = provider.GetRequiredService<IPodcastEpisodePoster>();
        var postModelFactory = provider.GetRequiredService<IPostModelFactory>();

        // Assert
        episodeProcessor.Should().NotBeNull();
        podcastEpisodePoster.Should().NotBeNull();
        postModelFactory.Should().NotBeNull();
    }

    private static ServiceCollection CreateIndexLikeContainer()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddLogging();
        services.AddCatalogueServices();
        services.AddSocialPostingServices();
        services.AddSingleton(Options.Create(new PostingCriteria
        {
            MinimumDuration = TimeSpan.FromMinutes(5),
            TweetDays = 7,
            RedditDays = 7,
            BlueSkyDays = 7,
            CategoriserDays = 7
        }));
        services.AddSingleton(Options.Create(new DelayedYouTubePublication
        {
            EvaluationThreshold = TimeSpan.FromDays(7)
        }));
        return services;
    }

    private static void StubCatalogueAndSocialDependencies(IServiceCollection services)
    {
        services.AddSingleton(Mock.Of<IAppleEpisodeRetrievalHandler>());
        services.AddSingleton(Mock.Of<IYouTubeEpisodeRetrievalHandler>());
        services.AddSingleton(Mock.Of<ISpotifyEpisodeRetrievalHandler>());
        services.AddSingleton(Mock.Of<IEpisodeRepository>());
        services.AddSingleton(Mock.Of<IPodcastRepository>());

        var subjects = new Mock<ISubjectsProvider>();
        subjects.Setup(x => x.GetAll()).Returns(EmptySubjects());
        services.AddSingleton(subjects.Object);
    }

    private static async IAsyncEnumerable<Subject> EmptySubjects()
    {
        await Task.CompletedTask;
        yield break;
    }
}

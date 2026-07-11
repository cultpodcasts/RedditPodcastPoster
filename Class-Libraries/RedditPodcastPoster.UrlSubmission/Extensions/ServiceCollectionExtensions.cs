using Microsoft.Extensions.DependencyInjection;
using RedditPodcastPoster.People.Extensions;
using RedditPodcastPoster.PodcastServices.Apple;
using RedditPodcastPoster.PodcastServices.Spotify.Categorisers;
using RedditPodcastPoster.PodcastServices.YouTube.Services;
using RedditPodcastPoster.UrlSubmission.Categorisation;
using RedditPodcastPoster.UrlSubmission.Factories;

namespace RedditPodcastPoster.UrlSubmission.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// UrlSubmission pipeline services. Registers People services required by
    /// <c>IPodcastProcessor</c> / <c>IPodcastAndEpisodeFactory</c> (<c>IEpisodeGuestEnricher</c>).
    /// Does not register episodes domain — callers must call <c>AddEpisodesDomain()</c>
    /// explicitly at the composition root (required for <c>IEpisodeEnricher</c> →
    /// <c>IPlatformEnrichmentApplicator</c>).
    /// </summary>
    public static IServiceCollection AddUrlSubmission(this IServiceCollection services)
    {
        return services
            .AddPeopleServices()
            .AddScoped<IUrlCategoriser, UrlCategoriser>()
            .AddScoped<IAppleUrlCategoriser, AppleUrlCategoriser>()
            .AddScoped<ISpotifyUrlCategoriser, SpotifyUrlCategoriser>()
            .AddScoped<IYouTubeUrlCategoriser, YouTubeUrlCategoriser>()
            .AddScoped<IUrlSubmitter, UrlSubmitter>()
            .AddScoped<IPodcastService, PodcastService>()
            .AddSingleton<IDescriptionHelper, DescriptionHelper>()
            .AddSingleton<IEpisodeHelper, EpisodeHelper>()
            .AddScoped<IEpisodeFactory, EpisodeFactory>()
            .AddScoped<IEpisodeEnricher, EpisodeEnricher>()
            .AddScoped<IPodcastAndEpisodeFactory, PodcastAndEpisodeFactory>()
            .AddScoped<IPodcastProcessor, PodcastProcessor>()
            .AddScoped<ICategorisedItemProcessor, CategorisedItemProcessor>()
            .AddScoped<IDiscoveryUrlSubmitter, DiscoveryUrlSubmitter>()
            .AddScoped<IDiscoveryResultProcessor, DiscoveryResultProcessor>()
            .AddSingleton<ISubmitResultAdaptor, SubmitResultAdaptor>();
    }
}
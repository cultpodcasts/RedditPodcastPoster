using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.Configuration.Extensions;
using RedditPodcastPoster.People.Extensions;
using RedditPodcastPoster.PodcastServices.Apple.Categorisers;
using RedditPodcastPoster.PodcastServices.Spotify.Categorisers;
using RedditPodcastPoster.PodcastServices.YouTube.Services;
using RedditPodcastPoster.UrlSubmission.Adaptors;
using RedditPodcastPoster.UrlSubmission.Categorisation;
using RedditPodcastPoster.UrlSubmission.Enrichers;
using RedditPodcastPoster.UrlSubmission.Factories;
using RedditPodcastPoster.UrlSubmission.Matching;
using RedditPodcastPoster.UrlSubmission.Processors;
using RedditPodcastPoster.UrlSubmission.Services;
using RedditPodcastPoster.UrlSubmission.Submitters;

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
    /// <param name="useRefreshMetaEnricher">
    /// When true (SubmitUrl CLI <c>-r</c>), resolve <see cref="IEpisodeEnricher"/> to
    /// <see cref="RefreshMetaEpisodeEnricher"/>. Otherwise use fill-missing
    /// <see cref="EpisodeEnricher"/>. Composition roots that never refresh meta omit this.
    /// </param>
    public static IServiceCollection AddUrlSubmission(
        this IServiceCollection services,
        bool useRefreshMetaEnricher = false)
    {
        services
            .AddSubmitContentTypes()
            .AddPeopleServices()
            .AddScoped<IUrlCategoriser, UrlCategoriser>()
            .AddScoped<IAppleUrlCategoriser, AppleUrlCategoriser>()
            .AddScoped<ISpotifyUrlCategoriser, SpotifyUrlCategoriser>()
            .AddScoped<IYouTubeUrlCategoriser, YouTubeUrlCategoriser>()
            .AddScoped<IUrlSubmitter, UrlSubmitter>()
            .AddScoped<IUrlMembershipLookup, UrlMembershipLookup>()
            .AddScoped<IPodcastService, PodcastService>()
            .AddSingleton<IDescriptionHelper, DescriptionHelper>()
            .AddSingleton<IEpisodeHelper, EpisodeHelper>()
            .AddScoped<IEpisodeFactory, EpisodeFactory>()
            .AddScoped<EpisodeEnricher>();

        if (useRefreshMetaEnricher)
        {
            services.AddScoped<IEpisodeEnricher, RefreshMetaEpisodeEnricher>();
        }
        else
        {
            services.AddScoped<IEpisodeEnricher>(sp => sp.GetRequiredService<EpisodeEnricher>());
        }

        return services
            .AddScoped<IPodcastAndEpisodeFactory, PodcastAndEpisodeFactory>()
            .AddScoped<IPodcastProcessor, PodcastProcessor>()
            .AddScoped<ICatalogueKindSubmitter, CatalogueKindSubmitter>()
            .AddScoped<ICategorisedItemProcessor, CategorisedItemProcessor>()
            .AddScoped<IDiscoveryUrlSubmitter, DiscoveryUrlSubmitter>()
            .AddScoped<IDiscoveryResultProcessor, DiscoveryResultProcessor>()
            .AddSingleton<ISubmitResultAdaptor, SubmitResultAdaptor>();
    }

    /// <summary>
    /// Binds <c>submitContentTypes</c> once. When the section is absent, <see cref="SubmitContentTypesOptions.Enabled"/> stays false.
    /// Hosts that already bound this options type are left unchanged so Api does not configure it twice.
    /// </summary>
    private static IServiceCollection AddSubmitContentTypes(this IServiceCollection services)
    {
        if (services.Any(descriptor => descriptor.ServiceType == typeof(IConfigureOptions<SubmitContentTypesOptions>)))
        {
            return services;
        }

        return services.BindConfiguration<SubmitContentTypesOptions>(SubmitContentTypesOptions.SectionName);
    }
}
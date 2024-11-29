using Microsoft.Extensions.DependencyInjection;
using RedditPodcastPoster.Configuration.Extensions;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.YouTube.Channel;
using RedditPodcastPoster.PodcastServices.YouTube.ChannelSnippets;
using RedditPodcastPoster.PodcastServices.YouTube.ChannelVideos;
using RedditPodcastPoster.PodcastServices.YouTube.Clients;
using RedditPodcastPoster.PodcastServices.YouTube.Configuration;
using RedditPodcastPoster.PodcastServices.YouTube.Enrichment;
using RedditPodcastPoster.PodcastServices.YouTube.Episode;
using RedditPodcastPoster.PodcastServices.YouTube.Factories;
using RedditPodcastPoster.PodcastServices.YouTube.Handlers;
using RedditPodcastPoster.PodcastServices.YouTube.Playlist;
using RedditPodcastPoster.PodcastServices.YouTube.Resolvers;
using RedditPodcastPoster.PodcastServices.YouTube.Services;
using RedditPodcastPoster.PodcastServices.YouTube.Strategies;
using RedditPodcastPoster.PodcastServices.YouTube.Video;

namespace RedditPodcastPoster.PodcastServices.YouTube.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddYouTubeServices(this IServiceCollection services, ApplicationUsage usage)
    {
        services.BindConfiguration<YouTubeSettings>("youtube");

        return services
            .AddScoped<IYouTubeEpisodeProvider, YouTubeEpisodeProvider>()
            .AddScoped<IYouTubeEpisodeEnricher, YouTubeEpisodeEnricher>()
            .AddScoped<IYouTubeItemResolver, YouTubeItemResolver>()
            .AddScoped<IYouTubePlaylistService, YouTubePlaylistService>()
            .AddScoped<IYouTubeVideoService, YouTubeVideoService>()
            .AddScoped<IYouTubeChannelVideoSnippetsService, YouTubeChannelVideoSnippetsService>()
            .AddScoped<IYouTubeChannelService, YouTubeChannelService>()
            .AddScoped<ISearchResultFinder, SearchResultFinder>()
            .AddScoped<IYouTubeChannelResolver, YouTubeChannelResolver>()
            .AddScoped<IYouTubeSearcher, YouTubeSearcher>()
            .AddSingleton<INoRedirectHttpClientFactory, NoRedirectHttpClientFactory>()
            .AddScoped<IYouTubeEpisodeRetrievalHandler, YouTubeEpisodeRetrievalHandler>()
            .AddScoped<IYouTubeChannelVideosService, YouTubeChannelVideosService>()
            .AddScoped<IYouTubeServiceFactory, YouTubeServiceFactory>()
            .AddScoped<IYouTubeApiKeyStrategy, YouTubeApiKeyStrategy>()
            .AddDateTimeService()
            .AddSingleton<IApplicationUsageProvider>(new ApplicationUsageProvider(usage))
            .AddScoped(s => s.GetService<IYouTubeServiceFactory>()!.Create(usage))
            .AddScoped(s => s.GetService<IYouTubeServiceFactory>()!.Create())
            .AddScoped<ITolerantYouTubeChannelVideoSnippetsService, TolerantYouTubeChannelVideoSnippetsService>()
            .AddScoped<ICachedTolerantYouTubeChannelVideoSnippetsService,
                CachedTolerantYouTubeChannelVideoSnippetsService>();
    }
}
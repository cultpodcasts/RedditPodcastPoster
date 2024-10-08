﻿using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RedditPodcastPoster.PodcastServices.Abstractions;

namespace RedditPodcastPoster.PodcastServices.YouTube.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddYouTubeServices(this IServiceCollection services, IConfiguration config)
    {
        YouTubeServiceFactory.AddYouTubeService(services);

        services
            .AddOptions<YouTubeSettings>().Bind(config.GetSection("youtube"));

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
            ;
    }
}
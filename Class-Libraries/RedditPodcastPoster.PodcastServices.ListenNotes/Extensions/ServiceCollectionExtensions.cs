﻿using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RedditPodcastPoster.Configuration.Extensions;
using RedditPodcastPoster.PodcastServices.ListenNotes.Configuration;
using RedditPodcastPoster.PodcastServices.ListenNotes.Factories;

namespace RedditPodcastPoster.PodcastServices.ListenNotes.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddListenNotes(
        this IServiceCollection services,
        IConfiguration config)
    {
        services.BindConfiguration<ListenNotesOptions>("listenNotes");

        return services
            .AddScoped<IClientFactory, ClientFactory>()
            .AddScoped<IListenNotesSearcher, ListenNotesSearcher>();
    }
}
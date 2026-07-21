using Microsoft.Extensions.DependencyInjection;
using RedditPodcastPoster.Configuration.Extensions;
using RedditPodcastPoster.PodcastServices.Taddy.Configuration;
using RedditPodcastPoster.PodcastServices.Taddy.Dtos;
using RedditPodcastPoster.PodcastServices.Taddy.Extensions;
using RedditPodcastPoster.PodcastServices.Taddy.Factories;
using RedditPodcastPoster.PodcastServices.Taddy.Models;
using RedditPodcastPoster.PodcastServices.Taddy.Searchers;

namespace RedditPodcastPoster.PodcastServices.Taddy.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddTaddy(this IServiceCollection services)
    {
        return services
            .AddScoped<ITaddySearcher, TaddySearcher>()
            .AddSingleton<ITaddyClientFactory, TaddyClientFactory>()
            .AddScoped(s => s.GetService<ITaddyClientFactory>()!.Create())
            .BindConfiguration<TaddyOptions>("taddy");
    }
}

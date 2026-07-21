using Microsoft.Extensions.DependencyInjection;
using RedditPodcastPoster.Configuration.Extensions;
using RedditPodcastPoster.PodcastServices.ListenNotes.Configuration;
using RedditPodcastPoster.PodcastServices.ListenNotes.Extensions;
using RedditPodcastPoster.PodcastServices.ListenNotes.Factories;
using RedditPodcastPoster.PodcastServices.ListenNotes.Model;
using RedditPodcastPoster.PodcastServices.ListenNotes.Searchers;

namespace RedditPodcastPoster.PodcastServices.ListenNotes.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddListenNotes(this IServiceCollection services)
    {
        return services
            .AddScoped<IClientFactory, ClientFactory>()
            .AddScoped<IListenNotesSearcher, ListenNotesSearcher>()
            .BindConfiguration<ListenNotesOptions>("listenNotes");
    }
}

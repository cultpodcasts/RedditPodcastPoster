using Microsoft.Extensions.DependencyInjection;
using RedditPodcastPoster.Indexing.Extensions;
using RedditPodcastPoster.Indexing.Models;
using RedditPodcastPoster.Indexing.Services;
using RedditPodcastPoster.People.Extensions;

namespace RedditPodcastPoster.Indexing.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <c>IIndexer</c>. Also registers People services required by
    /// guest enrichment (<c>IEpisodeGuestEnricher</c>).
    /// </summary>
    public static IServiceCollection AddIndexer(this IServiceCollection services)
    {
        return services
            .AddPeopleServices()
            .AddScoped<IIndexer, Indexer>();
    }
}

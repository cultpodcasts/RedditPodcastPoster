using Microsoft.Extensions.DependencyInjection;
using RedditPodcastPoster.Configuration.Extensions;

namespace RedditPodcastPoster.Search.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSearch(this IServiceCollection services)
    {
        return services
            .AddScoped<ISearchIndexClientFactory, SearchIndexClientFactory>()
            .AddScoped<ISearchIndexerClientFactory, SearchIndexerClientFactory>()
            .AddScoped<ISearchClientFactory, SearchClientFactory>()
            .AddScoped<ISearchIndexerService, SearchIndexerService>()
            .AddScoped(s => s.GetService<ISearchIndexClientFactory>()!.Create())
            .AddScoped(s => s.GetService<ISearchIndexerClientFactory>()!.Create())
            .AddScoped(s => s.GetService<ISearchClientFactory>()!.Create())
            .BindConfiguration<SearchIndexConfig>("searchIndex");
    }
}
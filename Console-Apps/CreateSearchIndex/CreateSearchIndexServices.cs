using Microsoft.Extensions.DependencyInjection;
using RedditPodcastPoster.Configuration;
using RedditPodcastPoster.Configuration.Extensions;
using RedditPodcastPoster.EntitySearchIndexer.Extensions;
using RedditPodcastPoster.Persistence.Configuration;
using RedditPodcastPoster.Persistence.Extensions;
using RedditPodcastPoster.Search.Extensions;

namespace CreateSearchIndex;

/// <summary>
/// Service registration for the CreateSearchIndex console app, shared by Program.cs and the composition tests so a
/// missing registration (for example the film push behind <see cref="IRebuildFilmPush"/>) fails a unit test instead
/// of crashing every CreateSearchIndex run at startup.
/// </summary>
public static class CreateSearchIndexServices
{
    public static IServiceCollection AddCreateSearchIndexConsoleServices(this IServiceCollection services)
    {
        services
            .AddLogging()
            .AddScoped<CreateSearchIndexProcessor>()
            .AddScoped<IRebuildFilmPush, RebuildFilmPush>()
            .AddRepositories()
            .AddFilmSearchPush()
            .AddSearch()
            .BindConfiguration<CosmosDbSettings>("cosmosdb");
        return services;
    }
}

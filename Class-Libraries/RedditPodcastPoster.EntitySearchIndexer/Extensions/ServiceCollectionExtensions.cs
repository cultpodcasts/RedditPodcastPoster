using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RedditPodcastPoster.EntitySearchIndexer.D1Search;
using RedditPodcastPoster.EntitySearchIndexer.FilmSearchPush;
using RedditPodcastPoster.EntitySearchIndexer.Models;
using RedditPodcastPoster.EntitySearchIndexer.Services;
using RedditPodcastPoster.Search.Extensions;

namespace RedditPodcastPoster.EntitySearchIndexer.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddEpisodeSearchIndexerService(this IServiceCollection services)
    {
        return services
            .AddSearch()
            .AddD1SearchSync()
            .AddScoped<IPlayableSearchDocumentSwap, PlayableSearchDocumentSwap>()
            .AddScoped<IEpisodeSearchIndexerService, EpisodeSearchIndexerService>();
    }

    /// <summary>
    /// The film <c>_ts</c> push (<see cref="IFilmSearchPusher"/>). Needs <c>AddRepositories()</c> for the films
    /// repository and the LookUps watermark store. Registers <see cref="TimeProvider.System"/> if no
    /// <see cref="TimeProvider"/> is registered yet. Used by the FilmSearchPusher console, the Indexer's scheduled
    /// refresh and CreateSearchIndex after a teardown (#1408).
    /// </summary>
    public static IServiceCollection AddFilmSearchPush(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        return services
            .AddSearch()
            .AddScoped<IFilmSearchIndexGateway, FilmSearchIndexGateway>()
            .AddScoped<IFilmSearchPusher, FilmSearchPusher>();
    }
}

using Microsoft.Extensions.DependencyInjection;
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
            .AddScoped<IPlayableSearchDocumentSwap, PlayableSearchDocumentSwap>()
            .AddScoped<IEpisodeSearchIndexerService, EpisodeSearchIndexerService>();
    }

    /// <summary>
    /// The film <c>_ts</c> push (<see cref="IFilmSearchPusher"/>). Needs <c>AddRepositories()</c> for the films
    /// repository and the LookUps watermark store, and a <see cref="TimeProvider"/>.
    /// </summary>
    public static IServiceCollection AddFilmSearchPush(this IServiceCollection services)
    {
        return services
            .AddSearch()
            .AddScoped<IFilmSearchIndexGateway, FilmSearchIndexGateway>()
            .AddScoped<IFilmSearchPusher, FilmSearchPusher>();
    }
}

using Microsoft.Extensions.DependencyInjection;
using RedditPodcastPoster.Persistence.Abstractions;

namespace RedditPodcastPoster.Subjects.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSubjectServices(this IServiceCollection services)
    {
        return services
            .AddSingleton<ISubjectRepository, SubjectRepository>()
            .AddScoped<ISubjectService, SubjectService>()
            .AddScoped<ISubjectEnricher, SubjectEnricher>()
            .AddScoped<ISubjectMatcher, SubjectMatcher>()
            .AddSingleton<IRecycledFlareIdProvider, RecycledFlareIdProvider>()
            .AddScoped<ICategoriser, Categoriser>()
            .AddScoped<IRecentPodcastEpisodeCategoriser, RecentPodcastEpisodeCategoriser>()
            .AddScoped<ISubjectFactory, SubjectFactory>();
    }

    public static IServiceCollection AddCachedSubjectProvider(this IServiceCollection services)
    {
        return services
            .AddSingleton<ISubjectsProvider, CachedSubjectProvider>();
    }

    public static IServiceCollection AddSubjectProvider(this IServiceCollection services)
    {
        return services
            .AddSingleton<ISubjectsProvider, SubjectRepository>();
    }
}
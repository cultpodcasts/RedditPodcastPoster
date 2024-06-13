using Microsoft.Extensions.DependencyInjection;
using RedditPodcastPoster.Persistence.Abstractions;

namespace RedditPodcastPoster.Subjects.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSubjectServices(this IServiceCollection services)
    {
        return services
            .AddSingleton<ICachedSubjectRepository, CachedSubjectRepository>()
            .AddSingleton<ISubjectRepository, SubjectRepository>()
            .AddScoped<ISubjectService, SubjectService>()
            .AddScoped<ISubjectEnricher, SubjectEnricher>()
            .AddScoped<ISubjectMatcher, SubjectMatcher>()
            .AddSingleton<IRecycledFlareIdProvider, RecycledFlareIdProvider>()
            .AddScoped<ICategoriser, Categoriser>();
    }
}
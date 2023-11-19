using Microsoft.Extensions.DependencyInjection;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence.Abstractions;
using RedditPodcastPoster.Persistence.Extensions;

namespace RedditPodcastPoster.Subjects.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSubjectServices(this IServiceCollection services)
    {
        return services
            .AddSingleton<ICachedSubjectRepository, CachedSubjectRepository>()
            .AddRepository<Subject>()
            .AddScoped<ISubjectService, SubjectService>()
            .AddScoped<ISubjectEnricher, SubjectEnricher>()
            .AddScoped<ISubjectMatcher, SubjectMatcher>();
    }
}
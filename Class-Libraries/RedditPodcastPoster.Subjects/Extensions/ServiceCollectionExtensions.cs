using Microsoft.Extensions.DependencyInjection;
using RedditPodcastPoster.Persistence.Abstractions;
using RedditPodcastPoster.Subjects.Factories;
using RedditPodcastPoster.Subjects.HashTags;

namespace RedditPodcastPoster.Subjects.Extensions;

public static class ServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddSubjectServices()
        {
            return services
                .AddSingleton<ISubjectRepository>(s =>
                {
                    var containerFactory = s.GetRequiredService<ICosmosDbContainerFactory>();
                    var logger = s.GetRequiredService<Microsoft.Extensions.Logging.ILogger<SubjectRepository>>();
                    return new SubjectRepository(containerFactory.CreateSubjectsContainer(), logger);
                })
                .AddScoped<ISubjectService, SubjectService>()
                .AddScoped<ISubjectEnricher, SubjectEnricher>()
                .AddScoped<ISubjectMatcher, SubjectMatcher>()
                .AddSingleton<IRecycledFlareIdProvider, RecycledFlareIdProvider>()
                .AddScoped<ICategoriser, Categoriser>()
                .AddScoped<IRecentPodcastEpisodeCategoriser, RecentPodcastEpisodeCategoriser>()
                .AddScoped<ISubjectFactory, SubjectFactory>()
                .AddScoped<IHashTagProvider, HashTagProvider>()
                .AddSingleton<ICachedSubjectProvider, CachedSubjectProvider>();
        }

        public IServiceCollection AddCachedSubjectProvider()
        {
            return services
                .AddSingleton<ISubjectsProvider, CachedSubjectProvider>();
        }

        public IServiceCollection AddSubjectProvider()
        {
            return services
                .AddSingleton<ISubjectsProvider>(s =>
                    (ISubjectsProvider)s.GetRequiredService<ISubjectRepository>());
        }
    }
}
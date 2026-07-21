using Microsoft.Extensions.DependencyInjection;
using RedditPodcastPoster.Persistence.Abstractions.Factories;
using RedditPodcastPoster.Persistence.Abstractions.Providers;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.Subjects.Categorisation;
using RedditPodcastPoster.Subjects.Enrichers;
using RedditPodcastPoster.Subjects.Factories;
using RedditPodcastPoster.Subjects.HashTags;
using RedditPodcastPoster.Subjects.Matching;
using RedditPodcastPoster.Subjects.Providers;
using RedditPodcastPoster.Subjects.Repositories;
using RedditPodcastPoster.Subjects.Services;

using RedditPodcastPoster.Models.Subjects;

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
using Microsoft.Extensions.DependencyInjection;
using RedditPodcastPoster.People.Factories;
using RedditPodcastPoster.Persistence.Abstractions.Factories;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;

namespace RedditPodcastPoster.People.Extensions;

public static class ServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddPeopleServices()
        {
            return services
                .AddSingleton<IPersonRepository>(s =>
                {
                    var containerFactory = s.GetRequiredService<ICosmosDbContainerFactory>();
                    var logger = s.GetRequiredService<Microsoft.Extensions.Logging.ILogger<PersonRepository>>();
                    return new PersonRepository(containerFactory.CreatePeopleContainer(), logger);
                })
                .AddScoped<IPersonService, PersonService>()
                .AddScoped<IEpisodeGuestEnricher, EpisodeGuestEnricher>()
                .AddScoped<IPersonGuestHandleResolver, PersonGuestHandleResolver>()
                .AddScoped<IPersonFactory, PersonFactory>();
        }
    }
}

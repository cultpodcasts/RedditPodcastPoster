using Microsoft.Extensions.DependencyInjection;
using RedditPodcastPoster.Cloudflare.Extensions;
using RedditPodcastPoster.Configuration.Extensions;
using RedditPodcastPoster.ContentPublisher.Configuration;
using RedditPodcastPoster.ContentPublisher.Publishers;
using RedditPodcastPoster.People.Extensions;

namespace RedditPodcastPoster.ContentPublisher.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers content publishers. Also registers People services required by
    /// <c>PeoplePublisher</c> (<c>IPersonRepository</c>).
    /// </summary>
    public static IServiceCollection AddContentPublishing(this IServiceCollection services)
    {
        return services
            .AddPeopleServices()
            .AddScoped<IHomepagePublisher, HomepagePublisher>()
            .AddScoped<ISubjectsPublisher, SubjectsPublisher>()
            .AddScoped<IPeoplePublisher, PeoplePublisher>()
            .AddScoped<IDiscoveryPublisher, DiscoveryPublisher>()
            .AddScoped<IDiscoveryInfoContentPublisher, DiscoveryInfoContentPublisher>()
            .AddScoped<ILanguagesPublisher, LanguagesPublisher>()
            .BindConfiguration<ContentOptions>("content")
            .AddCloudflareClients();
    }
}
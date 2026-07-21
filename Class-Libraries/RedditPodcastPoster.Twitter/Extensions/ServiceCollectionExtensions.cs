using Microsoft.Extensions.DependencyInjection;
using RedditPodcastPoster.Configuration.Extensions;
using RedditPodcastPoster.People.Extensions;
using RedditPodcastPoster.Twitter.Builders;
using RedditPodcastPoster.Twitter.Clients;
using RedditPodcastPoster.Twitter.Configuration;
using RedditPodcastPoster.Twitter.Dtos;
using RedditPodcastPoster.Twitter.Extensions;
using RedditPodcastPoster.Twitter.Managers;
using RedditPodcastPoster.Twitter.Models;
using RedditPodcastPoster.Twitter.Posters;

namespace RedditPodcastPoster.Twitter.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers Twitter posting services. Also registers People services required by
    /// <c>TweetBuilder</c> (<c>IPersonGuestHandleResolver</c>).
    /// </summary>
    public static IServiceCollection AddTwitterServices(this IServiceCollection services)
    {
        return services
            .AddPeopleServices()
            .AddScoped<ITweeter, Tweeter>()
            .AddScoped<ITwitterClient, TwitterClient>()
            .AddScoped<ITweetBuilder, TweetBuilder>()
            .AddScoped<ITweetPoster, TweetPoster>()
            .AddScoped<ITweetManager, TweetManager>()
            .BindConfiguration<TwitterOptions>("twitter");
    }
}

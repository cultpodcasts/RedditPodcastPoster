using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.Configuration.Extensions;
using RedditPodcastPoster.People.Extensions;
using RedditPodcastPoster.Twitter.Auth;
using RedditPodcastPoster.Twitter.Builders;
using RedditPodcastPoster.Twitter.Clients;
using RedditPodcastPoster.Twitter.Configuration;
using RedditPodcastPoster.Twitter.Managers;
using RedditPodcastPoster.Twitter.Posters;

namespace RedditPodcastPoster.Twitter.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers Twitter posting services. Also registers People services required by
    /// <c>TweetBuilder</c> (<c>IPersonGuestHandleResolver</c>). The OAuth 2.0 token pair is read from
    /// <c>IXOAuth2TokenStore</c>, which <c>AddRepositories()</c> registers.
    /// </summary>
    public static IServiceCollection AddTwitterServices(this IServiceCollection services)
    {
        services
            .AddPeopleServices()
            .AddScoped<ITweeter, Tweeter>()
            .AddScoped<ITweetBuilder, TweetBuilder>()
            .AddScoped<ITweetPoster, TweetPoster>()
            .AddScoped<ITweetManager, TweetManager>();
        services.AddXOAuth2TokenServices();
        services.AddTransient<TwitterAuthenticationHandler>();
        services.AddHttpClient<ITwitterClient, TwitterClient>(ConfigureXApiClient)
            .AddHttpMessageHandler<TwitterAuthenticationHandler>();
        return services;
    }

    /// <summary>
    /// The X OAuth 2.0 token endpoint client and access-token provider. Used by <see cref="AddTwitterServices"/>
    /// and by the XOAuth2TokenExchange console.
    /// </summary>
    public static IServiceCollection AddXOAuth2TokenServices(this IServiceCollection services)
    {
        services.BindConfiguration<TwitterOptions>("twitter");
        services.TryAddSingleton(TimeProvider.System);
        services.AddHttpClient<IXOAuth2TokenClient, XOAuth2TokenClient>(ConfigureXApiClient);
        services.TryAddTransient<IXOAuth2AccessTokenProvider, XOAuth2AccessTokenProvider>();
        services.TryAddTransient<IXOAuth2TokenExchanger, XOAuth2TokenExchanger>();
        return services;
    }

    private static void ConfigureXApiClient(IServiceProvider serviceProvider, HttpClient client)
    {
        var options = serviceProvider.GetRequiredService<IOptions<TwitterOptions>>().Value;
        client.BaseAddress = options.ApiBaseAddress;
        client.Timeout = TimeSpan.FromSeconds(options.RequestTimeoutSeconds > 0 ? options.RequestTimeoutSeconds : 15);
    }
}

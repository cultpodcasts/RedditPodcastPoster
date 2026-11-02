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
using RedditPodcastPoster.Twitter.Validators;

namespace RedditPodcastPoster.Twitter.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers Twitter posting services. Also registers People services required by
    /// <c>TweetBuilder</c> (<c>IPersonGuestHandleResolver</c>). The OAuth 2.0 token pair is read from
    /// <c>IXOAuth2TokenStore</c>, which <c>AddRepositories()</c> registers.
    /// Validates <see cref="TwitterOptions"/> per <see cref="TwitterAuthMode"/> at host startup
    /// (<c>ValidateOnStart</c>). Indexer, Api, and Poster call this; the XOAuth2TokenExchange /
    /// XOAuth2Authorize consoles use <see cref="AddXOAuth2TokenServices"/> only and keep their own
    /// missing-credential reporting.
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
        services.AddTwitterOptionsValidation(validateOnStart: true);
        services.AddTransient<TwitterAuthenticationHandler>();
        services.AddHttpClient<ITwitterClient, TwitterClient>(ConfigureXApiClient)
            .AddHttpMessageHandler<TwitterAuthenticationHandler>();
        return services;
    }

    /// <summary>
    /// The X OAuth 2.0 token endpoint client and access-token provider. Used by <see cref="AddTwitterServices"/>
    /// and by the XOAuth2TokenExchange / XOAuth2Authorize consoles. Does not register
    /// <see cref="TwitterOptions"/> validation — those consoles report missing credentials themselves.
    /// </summary>
    public static IServiceCollection AddXOAuth2TokenServices(this IServiceCollection services)
    {
        services.BindConfiguration<TwitterOptions>("twitter");
        services.TryAddSingleton(TimeProvider.System);
        services.AddHttpClient<IXOAuth2TokenClient, XOAuth2TokenClient>(ConfigureXApiClient);
        services.TryAddTransient<IXOAuth2AccessTokenProvider, XOAuth2AccessTokenProvider>();
        services.TryAddTransient<IXOAuth2TokenExchanger, XOAuth2TokenExchanger>();
        services.TryAddTransient<IXOAuth2Authorizer, XOAuth2Authorizer>();
        return services;
    }

    /// <summary>
    /// Registers <see cref="IValidateOptions{TwitterOptions}"/> for posting hosts.
    /// When <paramref name="validateOnStart"/> is true (Indexer, Api, Poster), a missing or
    /// AuthMode-mismatched credential fails host startup instead of the first tweet request.
    /// </summary>
    public static IServiceCollection AddTwitterOptionsValidation(
        this IServiceCollection services,
        bool validateOnStart = true)
    {
        services.TryAddSingleton<IValidateOptions<TwitterOptions>, TwitterOptionsValidator>();
        if (validateOnStart)
        {
            services.AddOptions<TwitterOptions>().ValidateOnStart();
        }

        return services;
    }

    private static void ConfigureXApiClient(IServiceProvider serviceProvider, HttpClient client)
    {
        var options = serviceProvider.GetRequiredService<IOptions<TwitterOptions>>().Value;
        client.BaseAddress = options.ApiBaseAddress;
        client.Timeout = TimeSpan.FromSeconds(options.RequestTimeoutSeconds > 0 ? options.RequestTimeoutSeconds : 15);
    }
}

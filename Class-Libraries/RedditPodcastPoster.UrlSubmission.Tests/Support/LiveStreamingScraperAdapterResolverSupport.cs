using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.AmazonPrime.Extensions;
using RedditPodcastPoster.BBC.Extensions;
using RedditPodcastPoster.Netflix.Extensions;
using RedditPodcastPoster.PodcastServices.Abstractions.Categorisers;
using RedditPodcastPoster.PodcastServices.Categorisers;
using RedditPodcastPoster.Vimeo.Extensions;

namespace RedditPodcastPoster.UrlSubmission.Tests.Support;

internal static class LiveStreamingScraperAdapterResolverSupport
{
    private static readonly Lazy<IServiceProvider> Provider = new(Build);

    public static INonPodcastServiceAdapterResolver Create() =>
        Provider.Value.GetRequiredService<INonPodcastServiceAdapterResolver>();

    private static IServiceProvider Build()
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Warning));
        services.AddHttpClient();
        services
            .AddBBCServices()
            .AddNetflixServices()
            .AddAmazonPrimeServices()
            .AddVimeoServices()
            .AddScoped<INonPodcastServiceAdapter, BbcNonPodcastServiceAdapter>()
            .AddScoped<INonPodcastServiceAdapterResolver, NonPodcastServiceAdapterResolver>();

        return services.BuildServiceProvider();
    }
}

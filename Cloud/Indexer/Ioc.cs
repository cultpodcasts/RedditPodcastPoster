using Azure;
using iTunesSearch.Library;
using Microsoft.Extensions.DependencyInjection;
using RedditPodcastPoster.BBC.Extensions;
using RedditPodcastPoster.Bluesky.Extensions;
using RedditPodcastPoster.Cloudflare.Extensions;
using RedditPodcastPoster.Common.Extensions;
using RedditPodcastPoster.Configuration;
using RedditPodcastPoster.Configuration.Extensions;
using RedditPodcastPoster.ContentPublisher.Extensions;
using RedditPodcastPoster.InternetArchive.Extensions;
using RedditPodcastPoster.Persistence.Extensions;
using RedditPodcastPoster.PodcastServices.Apple.Extensions;
using RedditPodcastPoster.PodcastServices.Extensions;
using RedditPodcastPoster.PodcastServices.Spotify.Extensions;
using RedditPodcastPoster.PodcastServices.YouTube.Configuration;
using RedditPodcastPoster.PodcastServices.YouTube.Extensions;
using RedditPodcastPoster.Reddit.Extensions;
using RedditPodcastPoster.Search.Extensions;
using RedditPodcastPoster.Subjects.Extensions;
using RedditPodcastPoster.Text.Extensions;
using RedditPodcastPoster.Twitter.Extensions;
using RedditPodcastPoster.UrlShortening.Extensions;

namespace Indexer;

public static class Ioc
{
    public static void ConfigureServices(IServiceCollection serviceCollection)
    {
        serviceCollection
            .AddRepositories()
            .AddTextSanitiser()
            .AddYouTubeServices(ApplicationUsage.Indexer)
            .AddSpotifyServices()
            .AddAppleServices()
            .AddCommonServices()
            .AddPodcastServices()
            .AddRemoteClient()
            .AddScoped(s => new iTunesSearchManager())
            .AddEliminationTerms()
            .AddRedditServices()
            .AddTwitterServices()
            .AddBlueskyServices()
            .AddSubjectServices()
            .AddCachedSubjectProvider()
            .AddScoped<IActivityMarshaller, ActivityMarshaller>()
            .AddContentPublishing()
            .AddCloudflareClients()
            .AddShortnerServices()
            .AddDateTimeService()
            .AddScoped<IIndexingStrategy, IndexingStrategy>()
            .AddSearch()
            .AddHttpClient()
            .BindConfiguration<IndexerOptions>("indexer")
            .BindConfiguration<PosterOptions>("poster")
            .AddPostingCriteria()
            .AddDelayedYouTubePublication()
            .AddBBCServices()
            .AddInternetArchiveServices();
    }
}
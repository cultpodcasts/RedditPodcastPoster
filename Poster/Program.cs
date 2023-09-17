using System.Text.Json;
using Azure;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RedditPodcastPoster.Common.Episodes;
using RedditPodcastPoster.Common.Persistence;
using RedditPodcastPoster.Common.Podcasts;
using RedditPodcastPoster.Common.Reddit;
using RedditPodcastPoster.Common.Text;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults(
        builder => { builder.Services.ConfigureFunctionsApplicationInsights(); })
    .ConfigureServices((context, services) =>
    {
        services.AddLogging()
            .AddApplicationInsightsTelemetryWorkerService()
            .ConfigureFunctionsApplicationInsights()
            .AddScoped<IDataRepository, CosmosDbRepository>()
            .AddScoped<ICosmosDbKeySelector, CosmosDbKeySelector>()
            .AddScoped<IPodcastRepository, PodcastRepository>()
            .AddScoped<IEpisodeResolver, EpisodeResolver>()
            .AddSingleton<ITextSanitiser, TextSanitiser>()
            .AddScoped<IRedditPostTitleFactory, RedditPostTitleFactory>()
            .AddScoped<IEpisodePostManager, EpisodePostManager>()
            .AddScoped<IResolvedPodcastEpisodeAdaptor, ResolvedPodcastEpisodeAdaptor>()
            .AddScoped<IResolvedPodcastEpisodePoster, ResolvedPodcastEpisodePoster>()
            .AddScoped<IRedditLinkPoster, RedditLinkPoster>()
            .AddScoped<IRedditEpisodeCommentFactory, RedditEpisodeCommentFactory>()
            .AddScoped<IRedditBundleCommentFactory, RedditBundleCommentFactory>()
            .AddSingleton(new JsonSerializerOptions
            {
                WriteIndented = true
            });

        services.AddHttpClient();

        RedditClientFactory.AddRedditClient(services);
        CosmosDbClientFactory.AddCosmosClient(services);

        services
            .AddOptions<RedditSettings>().Bind(context.Configuration.GetSection("reddit"));
        services
            .AddOptions<SubredditSettings>().Bind(context.Configuration.GetSection("subreddit"));
        services
            .AddOptions<CosmosDbSettings>().Bind(context.Configuration.GetSection("cosmosdb"));
    })
    .ConfigureLogging(logging => { logging.AllowAzureFunctionApplicationInsightsTraceLogging(); })
    .Build();

host.Run();
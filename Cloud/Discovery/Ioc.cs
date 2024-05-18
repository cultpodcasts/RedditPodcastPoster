﻿using Azure;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RedditPodcastPoster.Configuration.Extensions;
using RedditPodcastPoster.Discovery.Extensions;
using RedditPodcastPoster.Persistence.Extensions;
using RedditPodcastPoster.Subjects.Extensions;

namespace Discovery;

public static class Ioc
{
    public static void ConfigureServices(
        HostBuilderContext hostBuilderContext,
        IServiceCollection serviceCollection)
    {
        serviceCollection
            .AddLogging()
            .AddApplicationInsightsTelemetryWorkerService()
            .ConfigureFunctionsApplicationInsights()
            .AddRepositories()
            .AddSubjectServices()
            .AddDiscovery(hostBuilderContext.Configuration)
            .AddScoped<IActivityMarshaller, ActivityMarshaller>()
            .AddHttpClient();

        serviceCollection.BindConfiguration<DiscoverOptions>("discover");
    }
}
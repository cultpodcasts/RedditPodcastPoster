﻿using Api.Auth;
using iTunesSearch.Library;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RedditPodcastPoster.Common.Extensions;
using RedditPodcastPoster.Persistence.Extensions;
using RedditPodcastPoster.PodcastServices.Apple.Extensions;
using RedditPodcastPoster.PodcastServices.Extensions;
using RedditPodcastPoster.PodcastServices.Spotify.Extensions;
using RedditPodcastPoster.PodcastServices.YouTube.Extensions;
using RedditPodcastPoster.Subjects.Extensions;
using RedditPodcastPoster.Text.Extensions;
using RedditPodcastPoster.UrlSubmission.Extensions;

namespace Api;

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
            .AddAuth0(hostBuilderContext.Configuration)
            .AddRepositories(hostBuilderContext.Configuration)
            .AddTextSanitiser()
            .AddYouTubeServices(hostBuilderContext.Configuration)
            .AddSpotifyServices(hostBuilderContext.Configuration)
            .AddAppleServices()
            .AddPodcastServices(hostBuilderContext.Configuration)
            .AddRemoteClient()
            .AddScoped(s => new iTunesSearchManager())
            .AddSubjectServices()
            .AddUrlSubmission()
            .AddHttpClient();
    }
}
﻿using System.Reflection;
using CommandLine;
using iTunesSearch.Library;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RedditPodcastPoster.Common.Extensions;
using RedditPodcastPoster.Persistence.Extensions;
using RedditPodcastPoster.PodcastServices;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Apple.Extensions;
using RedditPodcastPoster.PodcastServices.Spotify.Extensions;
using RedditPodcastPoster.PodcastServices.YouTube.Extensions;
using RedditPodcastPoster.UrlSubmission.Extensions;
using SubmitUrl;

var builder = Host.CreateApplicationBuilder(args);


builder.Environment.ContentRootPath = Directory.GetCurrentDirectory();

builder.Configuration
    .AddJsonFile("appsettings.json", true)
    .AddEnvironmentVariables("RedditPodcastPoster_")
    .AddCommandLine(args)
    .AddSecrets(Assembly.GetExecutingAssembly());

builder.Services
    .AddLogging()
    .AddRepositories(builder.Configuration)
    .AddPodcastServices(builder.Configuration)
    .AddSpotifyServices(builder.Configuration)
    .AddAppleServices()
    .AddYouTubeServices(builder.Configuration)
    .AddScoped<IRemoteClient, RemoteClient>()
    .AddScoped(s => new iTunesSearchManager())
    .AddUrlSubmission()
    .AddScoped<SubmitUrlProcessor>()
    .AddHttpClient();

using var host = builder.Build();
return await Parser.Default.ParseArguments<SubmitUrlRequest>(args)
    .MapResult(async submitUrlRequest => await Run(submitUrlRequest), errs => Task.FromResult(-1)); // Invalid arguments

async Task<int> Run(SubmitUrlRequest request)
{
    var urlSubmitter = host.Services.GetService<SubmitUrlProcessor>()!;
    await urlSubmitter.Process(request);
    return 0;
}
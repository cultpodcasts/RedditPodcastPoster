﻿using System.Reflection;
using CultPodcasts.DatabasePublisher;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RedditPodcastPoster.Configuration.Extensions;
using RedditPodcastPoster.Persistence.Extensions;

var builder = Host.CreateApplicationBuilder(args);

builder.Environment.ContentRootPath = Directory.GetCurrentDirectory();

builder.Configuration
    .AddJsonFile("appsettings.json", true)
    .AddEnvironmentVariables("RedditPodcastPoster_")
    .AddCommandLine(args)
    .AddSecrets(Assembly.GetExecutingAssembly());

builder.Services
    .AddLogging()
    .AddFileRepository()
    .AddRepositories()
    .AddSafeFileWriter()
    .AddSingleton<PublicDatabasePublisher>();

using var host = builder.Build();
var processor = host.Services.GetService<PublicDatabasePublisher>();
await processor!.Run();
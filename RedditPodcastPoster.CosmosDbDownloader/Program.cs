﻿using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RedditPodcastPoster.Common;
using RedditPodcastPoster.Common.Persistence;
using RedditPodcastPoster.CosmosDbDownloader;

var builder = Host.CreateApplicationBuilder(args);

builder.Environment.ContentRootPath = Directory.GetCurrentDirectory();

builder.Configuration
    .AddJsonFile("appsettings.json", false)
    .AddEnvironmentVariables("RedditPodcastPoster_")
    .AddCommandLine(args)
    .AddSecrets(Assembly.GetExecutingAssembly());


builder.Services
    .AddLogging()
    .AddScoped<IFileRepository, FileRepository>()
    .AddScoped<ICosmosDbRepository, CosmosDbRepository>()
    .AddSingleton(new JsonSerializerOptions
    {
        WriteIndented = true
    })
    .AddScoped<CosmosDbDownloader>()
    .AddScoped<IFilenameSelector, FilenameSelector>()
    .AddScoped<ICosmosDbKeySelector, CosmosDbKeySelector>();

CosmosDbClientFactory.AddCosmosClient(builder.Services);
builder.Services
    .AddOptions<CosmosDbSettings>().Bind(builder.Configuration.GetSection("cosmosdb"));


using var host = builder.Build();
var processor = host.Services.GetService<CosmosDbDownloader>();
await processor.Run();
﻿using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModelTransformer;
using RedditPodcastPoster.Common.Persistence;

var builder = Host.CreateApplicationBuilder(args);

builder.Services
    .AddLogging()
    .AddScoped<SplitFileRepository>()
    .AddScoped<IFileRepositoryFactory, FileRepositoryFactory>()
    .AddScoped<ModelTransformer.ModelTransformer>()
    .AddSingleton(new JsonSerializerOptions
    {
        WriteIndented = true
    });

using var host = builder.Build();
var processor = host.Services.GetService<ModelTransformer.ModelTransformer>();
await processor!.Run();
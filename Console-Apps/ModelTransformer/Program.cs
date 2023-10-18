using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModelTransformer;
using RedditPodcastPoster.Persistence;

var builder = Host.CreateApplicationBuilder(args);

builder.Services
    .AddLogging()
    .AddScoped<ISplitFileRepository, SplitFileRepository>()
    .AddScoped<IFileRepositoryFactory, FileRepositoryFactory>()
    .AddScoped<ModelTransformProcessor>()
    .AddSingleton(new JsonSerializerOptions
    {
        WriteIndented = true
    });

using var host = builder.Build();
var processor = host.Services.GetService<ModelTransformProcessor>();
await processor!.Run();
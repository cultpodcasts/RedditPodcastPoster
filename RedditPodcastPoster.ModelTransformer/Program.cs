using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RedditPodcastPoster.Common.Persistence;
using RedditPodcastPoster.ModelTransformer;

var builder = Host.CreateApplicationBuilder(args);

builder.Services
    .AddLogging()
    .AddScoped<SplitFileRepository>()
    .AddScoped<IFilenameSelector, FilenameSelector>()
    .AddScoped<ModelTransformer>()
    .AddSingleton(new JsonSerializerOptions
    {
        WriteIndented = true
    });

using var host = builder.Build();
var processor = host.Services.GetService<ModelTransformer>();
await processor!.Run();
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RedditPodcastPoster.Common.Persistence;
using RedditPodcastPoster.ModelTransformer;
using FileRepository = RedditPodcastPoster.ModelTransformer.FileRepository;

var builder = Host.CreateApplicationBuilder(args);

builder.Services
    .AddLogging()
    .AddScoped<FileRepository>()
    .AddScoped<IFilenameSelector, FilenameSelector>()
    .AddScoped<ModelTransformer>()
    .AddSingleton(new JsonSerializerOptions
    {
        WriteIndented = true
    });

using var host = builder.Build();
var processor = host.Services.GetService<ModelTransformer>();
await processor!.Run();
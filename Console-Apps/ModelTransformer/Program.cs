using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModelTransformer;
using RedditPodcastPoster.Persistence;
using RedditPodcastPoster.Persistence.Abstractions;
using RedditPodcastPoster.Persistence.Extensions;

var builder = Host.CreateApplicationBuilder(args);

builder.Services
    .AddLogging()
    .AddFileRepository()
    .AddScoped<ISplitFileRepository, SplitFileRepository>()
    .AddSingleton<IJsonSerializerOptionsProvider, JsonSerializerOptionsProvider>()
    .AddSingleton<ModelTransformProcessor>();

using var host = builder.Build();
var processor = host.Services.GetService<ModelTransformProcessor>();
await processor!.Run();
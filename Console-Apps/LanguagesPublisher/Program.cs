using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RedditPodcastPoster.Configuration.Extensions;
using RedditPodcastPoster.ContentPublisher;
using RedditPodcastPoster.ContentPublisher.Extensions;

var appDirectory = AppContext.BaseDirectory;
var builder = Host.CreateApplicationBuilder(args);

builder.Environment.ContentRootPath = appDirectory;

builder.Configuration
    .AddJsonFile(Path.Combine(appDirectory, "appsettings.json"), true)
    .AddEnvironmentVariables("RedditPodcastPoster_")
    .AddCommandLine(args)
    .AddSecrets(Assembly.GetExecutingAssembly());

builder.Services
    .AddLogging()
    .AddContentPublishing();

using var host = builder.Build();

using var scope = host.Services.CreateScope();
var processor = scope.ServiceProvider.GetRequiredService<ILanguagesPublisher>();
var success = await processor.PublishLanguages();
return success ? 0 : 1;

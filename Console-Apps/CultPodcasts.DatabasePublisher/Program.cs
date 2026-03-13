using System.Reflection;
using CommandLine;
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
    .AddSingleton<PublicDatabasePublisher>()
    .AddSingleton<PublicDatabasePublisherV2>();

using var host = builder.Build();

return await Parser.Default.ParseArguments<PublisherRequest>(args)
    .MapResult(async request => await Run(request),
        errs => Task.FromResult(-1));

async Task<int> Run(PublisherRequest request)
{
    if (request.UseV2)
    {
        var publisher = host.Services.GetRequiredService<PublicDatabasePublisherV2>();
        await publisher.Run();
    }
    else
    {
        var publisher = host.Services.GetRequiredService<PublicDatabasePublisher>();
        await publisher.Run();
    }

    return 0;
}
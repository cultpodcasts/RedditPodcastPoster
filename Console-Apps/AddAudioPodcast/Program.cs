using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using AddAudioPodcast;
using CommandLine;
using RedditPodcastPoster.Configuration.Extensions;
using RedditPodcastPoster.Configuration;

if (VersionInfo.TryHandleVersionRequest(args))
{
    return 0;
}

var builder = Host.CreateApplicationBuilder(args);

builder.Environment.ContentRootPath = Directory.GetCurrentDirectory();

builder.Configuration
    .AddJsonFile("appsettings.json", true)
    .AddEnvironmentVariables("RedditPodcastPoster_")
    .AddCommandLine(args)
    .AddSecrets(Assembly.GetExecutingAssembly());

builder.Services.AddAddAudioPodcastConsoleServices();

using var host = builder.Build();

return await Parser.Default.ParseArguments<AddAudioPodcastRequest>(args)
    .MapResult(async addAudioPodcastRequest => await Run(addAudioPodcastRequest),
        errs => Task.FromResult(-1));

async Task<int> Run(AddAudioPodcastRequest request)
{
    var podcastProcessor = host.Services.GetService<AddAudioPodcastProcessor>()!;
    await podcastProcessor.Create(request);
    return 0;
}

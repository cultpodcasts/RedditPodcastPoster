using CommandLine;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RedditPodcastPoster.Common.PodcastServices.YouTube;
using System.Reflection;
using RedditPodcastPoster.Common;
using YouTubeChannelIdSearch;

var builder = Host.CreateApplicationBuilder(args);

builder.Environment.ContentRootPath = Directory.GetCurrentDirectory();

builder.Configuration
    .AddJsonFile("appsettings.json", true)
    .AddEnvironmentVariables("RedditPodcastPoster_")
    .AddCommandLine(args)
    .AddSecrets(Assembly.GetExecutingAssembly());

builder.Services
    .AddScoped<IYouTubeChannelResolver, YouTubeChannelResolver>();

YouTubeServiceFactory.AddYouTubeService(builder.Services);

builder.Services
    .AddOptions<YouTubeSettings>().Bind(builder.Configuration.GetSection("youtube"));

using var host = builder.Build();

return await Parser.Default.ParseArguments<Args>(args)
    .MapResult(async processRequest => await Run(processRequest), errs => Task.FromResult(-1)); // Invalid arguments

async Task<int> Run(Args request)
{
    var youTubeChannelResolver = host.Services.GetService<IYouTubeChannelResolver>()!;
    var match = await youTubeChannelResolver.FindChannel(request.ChannelName,
        request.MostRecentUploadedVideoTitle);
    if (match != null)
    {
        Console.WriteLine($"Found channel-id: {match}");
        return 0;
    }

    Console.WriteLine("Failed to match channel");
    return -1;
}
using System.Diagnostics;
using System.Net.WebSockets;
using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RedditPodcastPoster.Auth0;
using RedditPodcastPoster.Auth0.Extensions;
using RedditPodcastPoster.Configuration.Extensions;
using RedditPodcastPoster.Persistence.Abstractions;
using RedditPodcastPoster.Persistence.Extensions;

var builder = Host.CreateApplicationBuilder(args);

builder.Environment.ContentRootPath = Directory.GetCurrentDirectory();

builder.Configuration.SetBasePath(GetBasePath());

builder.Configuration
    .AddJsonFile("appsettings.json", false)
    .AddEnvironmentVariables("RedditPodcastPoster_")
    .AddCommandLine(args)
    .AddSecrets(Assembly.GetExecutingAssembly());

builder.Services
    .AddLogging()
    .AddRepositories()
    .AddHttpClient();

builder.Services.AddPostingCriteria();
builder.Services.AddDelayedYouTubePublication();


using var host = builder.Build();

var component = host.Services.GetService<IPodcastRepository>()!;

var podcastsIds= await component.GetAllBy(x=>x.IndexAllEpisodes && x.YouTubeChannelId!=string.Empty, podcast => new{id=podcast.Id}).ToArrayAsync();

var guid = Guid.Parse("c9bdd718-70ea-48c7-ae41-feef7af9ab4f");
var podcast = podcastsIds.Single(x => x.id== guid);
var position = Array.IndexOf(podcastsIds.ToArray(), podcast);
Console.WriteLine($"{position}/{podcastsIds.Length}");

//var expiredPodcasts = podcasts.Where(x => DateTime.UtcNow - x.Episodes.MaxBy(x => x.Release).Release > TimeSpan.FromDays(180));
//foreach (var expiredPodcast in expiredPodcasts)
//{
//    Console.WriteLine($"'{expiredPodcast.Name}'");
//}

return;

string GetBasePath()
{
    using var processModule = Process.GetCurrentProcess().MainModule;
    return Path.GetDirectoryName(processModule?.FileName) ?? throw new InvalidOperationException();
}
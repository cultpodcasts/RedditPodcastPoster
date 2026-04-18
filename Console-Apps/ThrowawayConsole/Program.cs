using System.Diagnostics;
using System.Reflection;
using Microsoft.Azure.Cosmos.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RedditPodcastPoster.Common.Extensions;
using RedditPodcastPoster.Configuration.Extensions;
using RedditPodcastPoster.Persistence.Abstractions;
using RedditPodcastPoster.Persistence.Extensions;
using RedditPodcastPoster.Subjects.Extensions;
using RedditPodcastPoster.Text.Extensions;

var builder = Host.CreateApplicationBuilder(args);

builder.Configuration.SetBasePath(GetBasePath());

builder.Configuration
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", true)
    .AddEnvironmentVariables("RedditPodcastPoster_")
    .AddCommandLine(args)
    .AddSecrets(Assembly.GetExecutingAssembly());

builder.Services
    .AddLogging()
    .AddRepositories()
    .AddSubjectServices()
    .AddSubjectProvider()
    .AddTextSanitiser()
    .AddCommonServices()
    .AddPostingCriteria();

using var host = builder.Build();

var podcastRepository = host.Services.GetRequiredService<IPodcastRepository>();
var episodeRepository = host.Services.GetRequiredService<IEpisodeRepository>();

var podcast= await podcastRepository.GetBy(x => x.Name == args[0]);
if (podcast == null) 
{
    Console.WriteLine($"Podcast with name '{args[0]}' not found.");
    return;
}
var episodes =  episodeRepository.GetByPodcastId(podcast.Id);
await foreach (var episode in episodes)
{
    episode.SpotifyId= string.Empty;
    episode.Urls.Spotify = null;
    if (episode.Images != null)
    {
        episode.Images.Spotify = null;
    }
    await episodeRepository.Save(episode);
}


string GetBasePath()
{
    using var processModule = Process.GetCurrentProcess().MainModule;
    return Path.GetDirectoryName(processModule?.FileName) ?? throw new InvalidOperationException();
}
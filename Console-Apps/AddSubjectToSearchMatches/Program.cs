using System.Reflection;
using AddSubjectToSearchMatches;
using CommandLine;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RedditPodcastPoster.Configuration.Extensions;
using RedditPodcastPoster.Persistence.Extensions;
using RedditPodcastPoster.Search.Extensions;
using RedditPodcastPoster.Subjects.Extensions;
using RedditPodcastPoster.Text.Extensions;

var builder = Host.CreateApplicationBuilder(args);

builder.Environment.ContentRootPath = Directory.GetCurrentDirectory();

builder.Configuration
    .AddJsonFile("appsettings.json", true)
    .AddEnvironmentVariables("RedditPodcastPoster_")
    .AddCommandLine(args)
    .AddSecrets(Assembly.GetExecutingAssembly());

builder.Services
    .AddLogging()
    .AddScoped<Processor>()
    .AddRepositories()
    .AddSearch()
    .AddSubjectServices()
    .AddTextSanitiser()
    .AddCachedSubjectProvider()
    .AddHttpClient();

using var host = builder.Build();

return await Parser.Default.ParseArguments<Request>(args)
    .MapResult(async addAudioPodcastRequest => await Run(addAudioPodcastRequest),
        errs => Task.FromResult(-1)); // Invalid arguments

async Task<int> Run(Request request)
{
    var podcastProcessor = host.Services.GetService<Processor>()!;
    await podcastProcessor.Process(request);
    return 0;
}
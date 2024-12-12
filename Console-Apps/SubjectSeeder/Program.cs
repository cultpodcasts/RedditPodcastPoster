using System.Reflection;
using CommandLine;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RedditPodcastPoster.Cloudflare.Extensions;
using RedditPodcastPoster.Configuration.Extensions;
using RedditPodcastPoster.ContentPublisher.Extensions;
using RedditPodcastPoster.Persistence.Extensions;
using RedditPodcastPoster.Reddit;
using RedditPodcastPoster.Reddit.Extensions;
using RedditPodcastPoster.Subjects.Extensions;
using RedditPodcastPoster.Text.Extensions;
using SubjectSeeder;

var builder = Host.CreateApplicationBuilder(args);

builder.Environment.ContentRootPath = Directory.GetCurrentDirectory();

builder.Configuration
    .AddJsonFile("appsettings.json", true)
    .AddEnvironmentVariables("RedditPodcastPoster_")
    .AddCommandLine(args)
    .AddSecrets(Assembly.GetExecutingAssembly());

builder.Services
    .AddLogging()
    .AddRepositories()
    .AddSubjectServices()
    .AddCachedSubjectProvider()
    .AddSingleton<SubjectsSeeder>()
    .AddSubredditSettings()
    .AddContentPublishing()
    .AddTextSanitiser()
    .AddCloudflareClients()
    .BindConfiguration<RedditSettings>("reddit-moderator");

RedditClientFactory.AddRedditClient(builder.Services);

AdminRedditClientFactory.AddAdminRedditClient(builder.Services);

using var host = builder.Build();
return await Parser.Default.ParseArguments<SubjectRequest>(args)
    .MapResult(async subjectRequest => await Run(subjectRequest), errs => Task.FromResult(-1)); // Invalid arguments

async Task<int> Run(SubjectRequest request)
{
    var urlSubmitter = host.Services.GetService<SubjectsSeeder>()!;
    await urlSubmitter.Run(request);
    return 0;
}
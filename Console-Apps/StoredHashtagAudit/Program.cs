using System.Reflection;
using CommandLine;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Configuration;
using RedditPodcastPoster.Configuration.Extensions;
using RedditPodcastPoster.Persistence.Abstractions.Factories;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.Persistence.Extensions;
using RedditPodcastPoster.Subjects.Repositories;
using StoredHashtagAudit;

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

builder.Services
    .AddLogging()
    .AddRepositories()
    .AddSingleton<ISubjectRepository>(s =>
    {
        var containerFactory = s.GetRequiredService<ICosmosDbContainerFactory>();
        var logger = s.GetRequiredService<ILogger<SubjectRepository>>();
        return new SubjectRepository(containerFactory.CreateSubjectsContainer(), logger);
    })
    .AddSingleton<StoredHashtagAuditProcessor>();

using var host = builder.Build();

return await Parser.Default.ParseArguments<StoredHashtagAuditRequest>(args)
    .MapResult(async request =>
        {
            var processor = host.Services.GetRequiredService<StoredHashtagAuditProcessor>();
            var result = await processor.Run(request);
            return result.ExitCode;
        },
        errs => Task.FromResult(1));

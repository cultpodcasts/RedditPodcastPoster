using System.Reflection;
using CommandLine;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PeopleSeedApplicator;
using RedditPodcastPoster.Configuration.Extensions;
using RedditPodcastPoster.People.Extensions;
using RedditPodcastPoster.Persistence.Extensions;

var builder = Host.CreateApplicationBuilder(args);

builder.Environment.ContentRootPath = AppContext.BaseDirectory;

builder.Configuration
    .AddJsonFile(Path.Combine(AppContext.BaseDirectory, "appsettings.json"), true)
    .AddEnvironmentVariables("RedditPodcastPoster_")
    .AddCommandLine(args)
    .AddSecrets(Assembly.GetExecutingAssembly());

builder.Services
    .AddLogging()
    .AddRepositories()
    .AddPeopleServices()
    .AddScoped<PeopleSeedApplyProcessor>();

using var host = builder.Build();

return await Parser.Default.ParseArguments<PeopleSeedApplyRequest>(args)
    .MapResult(async request =>
    {
        using var scope = host.Services.CreateScope();
        var processor = scope.ServiceProvider.GetRequiredService<PeopleSeedApplyProcessor>();
        return await processor.Run(request);
    }, _ => Task.FromResult(-1));

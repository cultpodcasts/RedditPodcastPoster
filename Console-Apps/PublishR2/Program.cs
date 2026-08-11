using System.Reflection;
using CommandLine;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PublishR2;
using RedditPodcastPoster.Configuration.Extensions;
using RedditPodcastPoster.ContentPublisher.Extensions;
using RedditPodcastPoster.People.Extensions;
using RedditPodcastPoster.Persistence.Extensions;
using RedditPodcastPoster.Reddit.Extensions;
using RedditPodcastPoster.Subjects.Extensions;
using RedditPodcastPoster.Text.Extensions;

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
    .AddRepositories()
    .AddPeopleServices()
    .AddContentPublishing()
    .AddRedditServices()
    .AddTextSanitiser()
    .AddSubjectServices()
    .AddScoped<R2PublishProcessor>()
    .AddScoped<FlairPublishProcessor>();

using var host = builder.Build();
using var scope = host.Services.CreateScope();
var services = scope.ServiceProvider;

return await Parser.Default
    .ParseArguments<LanguagesRequest, PeopleRequest, SearchSuggestionsRequest, HomepageRequest, FlairsRequest,
        AllRequest>(args)
    .MapResult(
        (LanguagesRequest _) => RunR2(services, R2PublishTarget.Languages),
        (PeopleRequest _) => RunR2(services, R2PublishTarget.People),
        (SearchSuggestionsRequest _) => RunR2(services, R2PublishTarget.SearchSuggestions),
        (HomepageRequest _) => RunR2(services, R2PublishTarget.Homepage),
        (FlairsRequest _) => RunFlairs(services),
        (AllRequest _) => RunAll(services),
        _ => Task.FromResult(1));

async Task<int> RunR2(IServiceProvider sp, R2PublishTarget target)
{
    var processor = sp.GetRequiredService<R2PublishProcessor>();
    var success = await processor.Process(new R2PublishRequest { Target = target });
    return success ? 0 : 1;
}

async Task<int> RunFlairs(IServiceProvider sp)
{
    var processor = sp.GetRequiredService<FlairPublishProcessor>();
    await processor.Process(new FlairPublishRequest());
    return 0;
}

async Task<int> RunAll(IServiceProvider sp)
{
    var r2Exit = await RunR2(sp, R2PublishTarget.All);
    if (r2Exit != 0)
    {
        return r2Exit;
    }

    return await RunFlairs(sp);
}

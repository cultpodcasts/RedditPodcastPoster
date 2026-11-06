using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using CommandLine;
using SubmitUrl;
using RedditPodcastPoster.Configuration.Extensions;
using RedditPodcastPoster.Configuration;

if (VersionInfo.TryHandleVersionRequest(args))
{
    return 0;
}

return await Parser.Default.ParseArguments<SubmitUrlRequest>(args)
    .MapResult(async submitUrlRequest => await Run(submitUrlRequest, args), errs => Task.FromResult(-1));

async Task<int> Run(SubmitUrlRequest request, string[] hostArgs)
{
    var builder = Host.CreateApplicationBuilder(hostArgs);

    builder.Environment.ContentRootPath = Directory.GetCurrentDirectory();

    builder.Configuration
        .AddJsonFile("appsettings.json", true)
        .AddEnvironmentVariables("RedditPodcastPoster_")
        .AddCommandLine(hostArgs)
        .AddSecrets(Assembly.GetExecutingAssembly());

    builder.Services.AddSubmitUrlConsoleServices(request.RefreshMeta);

    using var host = builder.Build();
    var urlSubmitter = host.Services.GetRequiredService<SubmitUrlProcessor>();
    await urlSubmitter.Process(request);
    return 0;
}

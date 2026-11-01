using System.Reflection;
using CommandLine;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RedditPodcastPoster.Configuration;
using RedditPodcastPoster.Configuration.Extensions;
using RedditPodcastPoster.Persistence.Extensions;
using RedditPodcastPoster.Twitter.Auth;
using RedditPodcastPoster.Twitter.Extensions;
using XOAuth2TokenExchange;

// One-off: exchange the X posting account's OAuth 1.0a token for an OAuth 2.0 pair (X retires OAuth 1.0a on
// 1 Jan 2027). Dry run by default. Never prints token values.
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
    .AddXOAuth2TokenServices();

using var host = builder.Build();

return await Parser.Default.ParseArguments<XOAuth2TokenExchangeRequest>(args)
    .MapResult(async request => await Run(request),
        _ => Task.FromResult(-1));

async Task<int> Run(XOAuth2TokenExchangeRequest request)
{
    using var scope = host.Services.CreateScope();
    var exchanger = scope.ServiceProvider.GetRequiredService<IXOAuth2TokenExchanger>();
    var result = await exchanger.Run(request.Execute, CancellationToken.None);
    foreach (var line in result.Report)
    {
        Console.WriteLine(line);
    }

    return result.Succeeded ? 0 : 1;
}

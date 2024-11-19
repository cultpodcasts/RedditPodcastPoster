using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices.Marshalling;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RedditPodcastPoster.Auth0;
using RedditPodcastPoster.Auth0.Extensions;
using RedditPodcastPoster.Configuration.Extensions;

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
    .AddAuth0Validation()
    .AddHttpClient();

builder.Services.AddPostingCriteria(builder.Configuration);
builder.Services.AddDelayedYouTubePublication(builder.Configuration);


using var host = builder.Build();

var component = host.Services.GetService<IAuth0TokenValidator>()!;

var result = component.GetClaimsPrincipal(args[0]);
var model = result.ToClientPrincipal();
var subject = model.Subject;
var isCurate = model.HasScope("curate");
var x = 1;

string GetBasePath()
{
    using var processModule = Process.GetCurrentProcess().MainModule;
    return Path.GetDirectoryName(processModule?.FileName) ?? throw new InvalidOperationException();
}
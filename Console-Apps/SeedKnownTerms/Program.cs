using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RedditPodcastPoster.Common;
using RedditPodcastPoster.Persistence.Extensions;
using RedditPodcastPoster.Text.KnownTerms;
using SeedKnownTerms;

var builder = Host.CreateApplicationBuilder(args);

builder.Environment.ContentRootPath = Directory.GetCurrentDirectory();

builder.Configuration
    .AddJsonFile("appsettings.json", true)
    .AddEnvironmentVariables("RedditPodcastPoster_")
    .AddCommandLine(args)
    .AddSecrets(Assembly.GetExecutingAssembly());

builder.Services
    .AddLogging()
    .AddRepositories(builder.Configuration)
    .AddScoped<IKnownTermsRepository, KnownTermsRepository>()
    .AddSingleton<KnownTermsSeeder>();

using var host = builder.Build();
var processor = host.Services.GetService<KnownTermsSeeder>();
await processor!.Run();
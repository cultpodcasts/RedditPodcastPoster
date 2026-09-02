using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using CommandLine;
using iTunesSearch.Library;
using SubmitUrl;
using RedditPodcastPoster.BBC.Extensions;
using RedditPodcastPoster.Catalogue.Extensions;
using RedditPodcastPoster.Configuration.Extensions;
using RedditPodcastPoster.EdgeApi.Extensions;
using RedditPodcastPoster.EntitySearchIndexer.Extensions;
using RedditPodcastPoster.Episodes.Extensions;
using RedditPodcastPoster.InternetArchive.Extensions;
using RedditPodcastPoster.AmazonPrime.Extensions;
using RedditPodcastPoster.Netflix.Extensions;
using RedditPodcastPoster.Vimeo.Extensions;
using RedditPodcastPoster.People.Extensions;
using RedditPodcastPoster.Persistence.Extensions;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Abstractions.Clients;
using RedditPodcastPoster.PodcastServices.Apple.Extensions;
using RedditPodcastPoster.PodcastServices.Clients;
using RedditPodcastPoster.PodcastServices.Extensions;
using RedditPodcastPoster.PodcastServices.Spotify.Extensions;
using RedditPodcastPoster.PodcastServices.YouTube.Configuration;
using RedditPodcastPoster.PodcastServices.YouTube.Extensions;
using RedditPodcastPoster.Subjects.Extensions;
using RedditPodcastPoster.Text.Extensions;
using RedditPodcastPoster.UrlSubmission.Extensions;
using RedditPodcastPoster.Configuration;

var builder = Host.CreateApplicationBuilder(args);

builder.Environment.ContentRootPath = Directory.GetCurrentDirectory();

builder.Configuration
    .AddJsonFile("appsettings.json", true)
    .AddEnvironmentVariables("RedditPodcastPoster_")
    .AddCommandLine(args)
    .AddSecrets(Assembly.GetExecutingAssembly());

builder.Services
    .AddLogging()
    .AddEpisodesDomain()
    .AddRepositories()
    .AddCatalogueServices()
    .AddPodcastServices()
    .AddEdgeApiClient(bypassCertificateValidation: false)
    .AddSpotifyServices()
    .AddAppleServices()
    .AddYouTubeServices(ApplicationUsage.Cli)
    .AddScoped<IRemoteClient, RemoteClient>()
    .AddScoped(s => new iTunesSearchManager())
    .AddPeopleServices()
    .AddUrlSubmission()
    .AddSubjectServices()
    .AddCachedSubjectProvider()
    .AddTextSanitiser()
    .AddScoped<SubmitUrlProcessor>()
    .AddEpisodeSearchIndexerService()
    .AddBBCServices()
    .AddInternetArchiveServices()
    .AddVimeoServices()
    .AddNetflixServices()
    .AddAmazonPrimeServices()
    .AddHttpClient();

builder.Services.AddPostingCriteria();

using var host = builder.Build();
return await Parser.Default.ParseArguments<SubmitUrlRequest>(args)
    .MapResult(async submitUrlRequest => await Run(submitUrlRequest), errs =>
    {
        if (errs.Any(x => x is VersionRequestedError))
        {
            VersionInfo.PrintVersion();
            return Task.FromResult(0);
        }

        return Task.FromResult(-1);
    });

async Task<int> Run(SubmitUrlRequest request)
{
    if (request.Version)
    {
        VersionInfo.PrintVersion();
        return 0;
    }

    var urlSubmitter = host.Services.GetService<SubmitUrlProcessor>()!;
    await urlSubmitter.Process(request);
    return 0;
}

using System.Diagnostics;
using System.Net;
using System.Reflection;
using HtmlAgilityPack;
using iTunesSearch.Library;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RedditPodcastPoster.BBC.Extensions;
using RedditPodcastPoster.Cloudflare.Extensions;
using RedditPodcastPoster.Configuration.Extensions;
using RedditPodcastPoster.ContentPublisher.Extensions;
using RedditPodcastPoster.InternetArchive.Extensions;
using RedditPodcastPoster.Persistence.Abstractions;
using RedditPodcastPoster.Persistence.Extensions;
using RedditPodcastPoster.PodcastServices.Apple.Extensions;
using RedditPodcastPoster.PodcastServices.Extensions;
using RedditPodcastPoster.PodcastServices.Spotify.Extensions;
using RedditPodcastPoster.PodcastServices.YouTube.Configuration;
using RedditPodcastPoster.PodcastServices.YouTube.Extensions;
using RedditPodcastPoster.Reddit.Extensions;
using RedditPodcastPoster.Subjects.Extensions;
using RedditPodcastPoster.Text.Extensions;
using RedditPodcastPoster.UrlSubmission.Extensions;

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
    .AddRepositories()
    .AddContentPublishing()
    .AddCloudflareClients()
    .AddTextSanitiser()
    .AddPodcastServices()
    .AddSubjectServices()
    .AddRedditServices()
    .AddSpotifyServices()
    .AddUrlSubmission()
    .AddBBCServices()
    .AddInternetArchiveServices()
    .AddAppleServices()
    .AddSpotifyServices()
    .AddYouTubeServices(ApplicationUsage.Api)
    .AddScoped(s => new iTunesSearchManager())
    .AddHttpClient();


using var host = builder.Build();

var service = host.Services.GetService<IPodcastRepository>()!;
var httpClient = host.Services.GetService<HttpClient>();

Uri.TryCreate(args[1], UriKind.Absolute, out var url);
var x = await service.GetPodcast(new Guid(args[0]));


var document = new HtmlDocument();
httpClient.DefaultRequestHeaders.Add("user-agent",
    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/58.0.3029.110 Safari/537.3");
httpClient.DefaultRequestHeaders.Add("accept", "text/html");
var pageResponse = await httpClient.GetAsync(url);
if (pageResponse.StatusCode != HttpStatusCode.OK)
{
    throw new InvalidOperationException("Unable to get page");
}

document.Load(await pageResponse.Content.ReadAsStreamAsync());
var seasonHeadingIds = new[] { "Season_1_(2016–17)", "Season_2_(2017)", "Season_3_(2018–19)" };

foreach (var episode in x.Episodes)
{
    var components = episode.Title.Split("E").Select(x => x.TrimStart('S'));
    var season = int.Parse(components.First());
    var seasonEpisode = int.Parse(components.Skip(1).First().Split(" ")[0]);


    var headingId = seasonHeadingIds[season - 1];
    var node = document.DocumentNode.SelectSingleNode($"//h3[@id='{headingId}']");
    if (node != null)
    {
        node = node.ParentNode;
        while (node.Name != "table")
        {
            node = node.NextSibling;
        }

        var seasonEpisodeCells = node.SelectNodes($"tbody/tr/td[1]");
        var seasonTitleCells = node.SelectNodes($"tbody/tr/td[2]");
        //        var cell = seasonEpisodeCells.Where(x => x.InnerText.Trim() == seasonEpisode.ToString()).SingleOrDefault();
        var cell = seasonTitleCells.Where(x =>
            episode.Title.ToLowerInvariant()
                .EndsWith(HtmlEntity.DeEntitize(x.InnerText).ToLowerInvariant().Trim().Trim('\"'))).SingleOrDefault();
        if (cell != null)
        {
            var titleCell = cell;
            var title = titleCell.InnerText;
            var releaseDateCell = titleCell.NextSibling.SelectSingleNode("span");
            var date = HtmlEntity.DeEntitize(releaseDateCell.InnerText).Trim().Trim(new[] { '(', ')' });
            var releaseDate = DateOnly.ParseExact(date, "yyyy-MM-dd")
                .ToDateTime(TimeOnly.FromTimeSpan(TimeSpan.FromHours(12)));
            var descriptionCell = cell.ParentNode.NextSibling;
            var description = descriptionCell.InnerText.Trim();
            episode.Description = description;
            episode.Release = releaseDate;
        }
        else
        {
            //throw new InvalidOperationException("Not found");
        }
    }
    else
    {
        //throw new InvalidOperationException("Not found");
    }
}

await service.Save(x);
return;

string GetBasePath()
{
    using var processModule = Process.GetCurrentProcess().MainModule;
    return Path.GetDirectoryName(processModule?.FileName) ?? throw new InvalidOperationException();
}
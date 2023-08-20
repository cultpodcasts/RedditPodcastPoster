using System.Net.Http.Headers;
using ApplePodcastEpisodeEnricher;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RedditPodcastPoster.Common.Persistence;
using RedditPodcastPoster.Common.Podcasts;

var builder = Host.CreateApplicationBuilder(args);

builder.Services
    .AddLogging()
    .AddScoped<IDataRepository, FileRepository>()
    .AddScoped<IPodcastRepository, PodcastRepository>()
    .AddScoped<MissingFilesProcessor>()
    .AddHttpClient<MissingFilesProcessor>(c =>
    {
        c.BaseAddress = new Uri("https://amp-api.podcasts.apple.com/");
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(args[0], args[1]);
        c.DefaultRequestHeaders.Accept.Clear();
        c.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("*/*"));
        c.DefaultRequestHeaders.Referrer = new Uri("https://podcasts.apple.com/");
        c.DefaultRequestHeaders.Add("Origin", "https://podcasts.apple.com");
        c.DefaultRequestHeaders.UserAgent.Clear();
        c.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:109.0) Gecko/20100101 Firefox/118.0");

    });

using var host = builder.Build();
var processor = host.Services.GetService<MissingFilesProcessor>();
await processor.Run();
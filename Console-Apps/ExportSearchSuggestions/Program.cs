using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RedditPodcastPoster.Configuration.Extensions;
using RedditPodcastPoster.ContentPublisher.Builders;
using RedditPodcastPoster.Persistence.Extensions;
using RedditPodcastPoster.Subjects.Extensions;

// Read-only file export of the flat search-suggestions index (same builder as R2 publish).
// Prefer PublishR2 search-suggestions for production refresh.

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
    .AddSubjectServices()
    .AddScoped<ISearchSuggestionsIndexBuilder, SearchSuggestionsIndexBuilder>();

using var host = builder.Build();

var indexBuilder = host.Services.GetRequiredService<ISearchSuggestionsIndexBuilder>();
var outputPath = args.FirstOrDefault(a => !a.StartsWith("--"))
                  ?? "search-suggestions.json";

Console.WriteLine("Building search-suggestions index (subjects name+aliases; non-removed podcast names)...");
var corpus = await indexBuilder.BuildAsync();

var json = JsonSerializer.Serialize(corpus, new JsonSerializerOptions
{
    WriteIndented = true,
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
});

await File.WriteAllTextAsync(outputPath, json);
Console.WriteLine(
    $"Wrote flat suggestions index ({corpus.Entries.Length} entries) to '{Path.GetFullPath(outputPath)}'.");

return 0;

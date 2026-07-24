using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RedditPodcastPoster.Configuration.Extensions;
using RedditPodcastPoster.Persistence.Extensions;
using RedditPodcastPoster.Subjects.Extensions;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;

// Read-only, narrow-scope export for the flix search-typeahead prototype.
// Pulls ONLY: Subject.Name + Subject.Aliases (AssociatedSubjects deliberately excluded)
// and Podcast.Name. Never writes to Cosmos. Not a full-catalog dump (see CosmosDbDownloader
// for that) - this intentionally emits a single small JSON file with just the two fields
// the flix search box needs for typeahead.

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
    .AddSubjectServices();

using var host = builder.Build();

var subjectRepository = host.Services.GetRequiredService<ISubjectRepository>();
var podcastRepository = host.Services.GetRequiredService<IPodcastRepository>();

var outputPath = args.FirstOrDefault(a => !a.StartsWith("--"))
                  ?? "search-suggestions.json";

Console.WriteLine("Reading subjects (name + aliases only; associated-subjects excluded)...");
var subjects = new List<SubjectSuggestion>();
await foreach (var subject in subjectRepository.GetAll())
{
    if (string.IsNullOrWhiteSpace(subject.Name))
    {
        continue;
    }

    var aliases = (subject.Aliases ?? Array.Empty<string>())
        .Select(a => a.Trim())
        .Where(a => a.Length > 0)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .OrderBy(a => a, StringComparer.OrdinalIgnoreCase)
        .ToArray();

    subjects.Add(new SubjectSuggestion(subject.Name.Trim(), aliases));
}

subjects = subjects
    .OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
    .ToList();

Console.WriteLine($"Read {subjects.Count} subjects.");

Console.WriteLine("Reading podcast names only...");
var podcastNames = new List<string>();
await foreach (var podcast in podcastRepository.GetAll())
{
    if (string.IsNullOrWhiteSpace(podcast.Name))
    {
        continue;
    }

    if (podcast.Removed == true)
    {
        continue;
    }

    podcastNames.Add(podcast.Name.Trim());
}

podcastNames = podcastNames
    .Distinct(StringComparer.OrdinalIgnoreCase)
    .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
    .ToList();

Console.WriteLine($"Read {podcastNames.Count} podcast names.");

var corpus = new SuggestionsCorpus(
    DateTime.UtcNow,
    subjects.ToArray(),
    podcastNames.ToArray());

var json = JsonSerializer.Serialize(corpus, new JsonSerializerOptions
{
    WriteIndented = true,
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
});

await File.WriteAllTextAsync(outputPath, json);
Console.WriteLine($"Wrote suggestions corpus to '{Path.GetFullPath(outputPath)}'.");

return 0;

record SubjectSuggestion(string Name, string[] Aliases);

record SuggestionsCorpus(
    [property: JsonPropertyName("generatedAtUtc")] DateTime GeneratedAtUtc,
    [property: JsonPropertyName("subjects")] SubjectSuggestion[] Subjects,
    [property: JsonPropertyName("podcasts")] string[] Podcasts);

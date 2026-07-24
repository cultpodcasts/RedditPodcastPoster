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
// and Podcast.Name. Emits a flat match index: { type, canonical, searchText, alias? }
// with searchText already lowercase. Never writes to Cosmos. Not a full-catalog dump
// (see CosmosDbDownloader for that).

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
var entries = new List<SuggestionEntry>();
var seen = new HashSet<string>(StringComparer.Ordinal);

void Add(string type, string canonical, string sourceText, string? alias = null)
{
    var trimmedCanonical = canonical.Trim();
    var searchText = sourceText.Trim().ToLowerInvariant();
    if (trimmedCanonical.Length == 0 || searchText.Length == 0)
    {
        return;
    }

    var key = $"{type}\0{trimmedCanonical}\0{searchText}";
    if (!seen.Add(key))
    {
        return;
    }

    entries.Add(new SuggestionEntry(type, trimmedCanonical, searchText, alias));
}

var subjectCount = 0;
await foreach (var subject in subjectRepository.GetAll())
{
    if (string.IsNullOrWhiteSpace(subject.Name))
    {
        continue;
    }

    subjectCount++;
    var name = subject.Name.Trim();
    Add("subject", name, name);

    foreach (var rawAlias in subject.Aliases ?? Array.Empty<string>())
    {
        var alias = rawAlias.Trim();
        if (alias.Length == 0)
        {
            continue;
        }

        Add("subject", name, alias, alias);
    }
}

Console.WriteLine($"Read {subjectCount} subjects.");

Console.WriteLine("Reading podcast names only...");
var podcastCount = 0;
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

    podcastCount++;
    var name = podcast.Name.Trim();
    Add("podcast", name, name);
}

Console.WriteLine($"Read {podcastCount} podcast names.");

entries = entries
    .OrderBy(e => e.SearchText, StringComparer.Ordinal)
    .ThenBy(e => e.Type, StringComparer.Ordinal)
    .ThenBy(e => e.Canonical, StringComparer.Ordinal)
    .ToList();

var corpus = new SuggestionsCorpus(DateTime.UtcNow, entries.ToArray());

var json = JsonSerializer.Serialize(corpus, new JsonSerializerOptions
{
    WriteIndented = true,
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
});

await File.WriteAllTextAsync(outputPath, json);
Console.WriteLine(
    $"Wrote flat suggestions index ({entries.Count} entries) to '{Path.GetFullPath(outputPath)}'.");

return 0;

record SuggestionEntry(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("canonical")] string Canonical,
    [property: JsonPropertyName("searchText")] string SearchText,
    [property: JsonPropertyName("alias")] string? Alias);

record SuggestionsCorpus(
    [property: JsonPropertyName("generatedAtUtc")] DateTime GeneratedAtUtc,
    [property: JsonPropertyName("entries")] SuggestionEntry[] Entries);

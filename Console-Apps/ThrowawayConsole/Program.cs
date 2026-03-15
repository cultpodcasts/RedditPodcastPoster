using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using iTunesSearch.Library;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RedditPodcastPoster.BBC.Extensions;
using RedditPodcastPoster.Cloudflare.Extensions;
using RedditPodcastPoster.Configuration.Extensions;
using RedditPodcastPoster.ContentPublisher.Extensions;
using RedditPodcastPoster.InternetArchive.Extensions;
using RedditPodcastPoster.Models.V2;
using RedditPodcastPoster.Persistence.Abstractions;
using RedditPodcastPoster.Persistence.Extensions;
using RedditPodcastPoster.PodcastServices.Apple.Extensions;
using RedditPodcastPoster.PodcastServices.Extensions;
using RedditPodcastPoster.PodcastServices.Spotify.Extensions;
using RedditPodcastPoster.PodcastServices.YouTube.Configuration;
using RedditPodcastPoster.PodcastServices.YouTube.Extensions;
using RedditPodcastPoster.PushSubscriptions.Extensions;
using RedditPodcastPoster.Reddit.Extensions;
using RedditPodcastPoster.Subjects.Extensions;
using RedditPodcastPoster.Text.Extensions;
using RedditPodcastPoster.UrlShortening;
using RedditPodcastPoster.UrlShortening.Extensions;
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
    .AddSubjectServices()
    .AddSubjectProvider()
    .AddPushSubscriptions()
    .AddShortnerServices()
    .AddHttpClient();

using var host = builder.Build();

var podcastRepository = host.Services.GetRequiredService<IPodcastRepositoryV2>();
var episodeRepository = host.Services.GetRequiredService<IEpisodeRepository>();

var reparentEpisodes = builder.Configuration.GetValue<bool>("reparentEpisodes");
var fixAlreadyReparentedEpisodes = builder.Configuration.GetValue<bool>("fixAlreadyReparentedEpisodes");
var canonicalPodcastIdArg = builder.Configuration["canonicalPodcastId"];
var detachPodcastIdsArg = builder.Configuration["detachPodcastIds"];

if (reparentEpisodes || fixAlreadyReparentedEpisodes)
{
    if (!Guid.TryParse(canonicalPodcastIdArg, out var canonicalPodcastId))
    {
        throw new InvalidOperationException("When reparentEpisodes=true or fixAlreadyReparentedEpisodes=true, canonicalPodcastId must be provided as a valid guid.");
    }

    var detachPodcastIds = ParseGuids(detachPodcastIdsArg);
    if (detachPodcastIds.Count == 0)
    {
        throw new InvalidOperationException("When reparentEpisodes=true or fixAlreadyReparentedEpisodes=true, detachPodcastIds must include at least one guid.");
    }

    if (detachPodcastIds.Contains(canonicalPodcastId))
    {
        throw new InvalidOperationException("detachPodcastIds cannot include canonicalPodcastId.");
    }

    var canonicalPodcast = await podcastRepository.GetPodcast(canonicalPodcastId)
        ?? throw new InvalidOperationException($"Canonical podcast not found: {canonicalPodcastId}");

    var detachedPodcasts = new List<Podcast>();
    foreach (var detachId in detachPodcastIds)
    {
        var detachPodcast = await podcastRepository.GetPodcast(detachId);
        if (detachPodcast is null)
        {
            if (fixAlreadyReparentedEpisodes)
            {
                Console.WriteLine($"Warning: detach podcast not found, will still process episodes by partition id: {detachId}");
                continue;
            }

            throw new InvalidOperationException($"Detach podcast not found: {detachId}");
        }

        detachedPodcasts.Add(detachPodcast);
    }

    if (detachedPodcasts.Count == 0 && reparentEpisodes)
    {
        Console.WriteLine("No existing detached podcasts were found for the provided detachPodcastIds.");
        return;
    }

    var existingDetachedPodcastIds = detachedPodcasts
        .Select(x => x.Id)
        .ToHashSet();

    if (fixAlreadyReparentedEpisodes && existingDetachedPodcastIds.Count > 0)
    {
        var existingIds = string.Join(", ", existingDetachedPodcastIds.OrderBy(x => x));
        throw new InvalidOperationException(
            "Safety check failed for fixAlreadyReparentedEpisodes: all detachPodcastIds must be non-existent, " +
            $"but these still exist: {existingIds}");
    }

    if (reparentEpisodes)
    {
        var validationErrors = ValidateCanonical(canonicalPodcast, detachedPodcasts);
        if (validationErrors.Count > 0)
        {
            Console.WriteLine("Validation failed. Re-parenting was not performed.");
            foreach (var validationError in validationErrors)
            {
                Console.WriteLine($" - {validationError}");
            }

            return;
        }
    }
    else
    {
        Console.WriteLine("fixAlreadyReparentedEpisodes=true: skipping podcast metadata validation and repairing episode partition placement by IDs.");
    }

    var moved = 0;
    var deletedFromOldPartition = 0;
    var remainingInOldPartitions = 0;

    foreach (var detachPodcastId in detachPodcastIds)
    {
        var sourceEpisodes = new List<Episode>();
        await foreach (var episode in episodeRepository.GetByPodcastId(detachPodcastId))
        {
            sourceEpisodes.Add(episode);
        }

        foreach (var episode in sourceEpisodes)
        {
            episode.SetPodcastProperties(canonicalPodcast);
            await episodeRepository.Save(episode);
            moved++;

            await episodeRepository.Delete(detachPodcastId, episode.Id);
            deletedFromOldPartition++;
        }

        await foreach (var _ in episodeRepository.GetByPodcastId(detachPodcastId))
        {
            remainingInOldPartitions++;
        }
    }

    Console.WriteLine($"Re-parent complete. CanonicalPodcastId={canonicalPodcastId}, DetachedPodcastCount={detachPodcastIds.Count}, EpisodesMoved={moved}, OldPartitionDeletes={deletedFromOldPartition}, RemainingInOldPartitions={remainingInOldPartitions}");
    return;
}

var podcasts = new List<DuplicatePodcastItem>();

await foreach (var podcast in podcastRepository.GetAll())
{
    if (string.IsNullOrWhiteSpace(podcast.Name))
    {
        continue;
    }

    podcasts.Add(new DuplicatePodcastItem(
        podcast.Name.Trim(),
        podcast.Id,
        podcast.AppleId,
        podcast.SpotifyId,
        podcast.YouTubeChannelId,
        null));
}

var duplicateCandidates = podcasts
    .GroupBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
    .Where(g => g.Count() > 1)
    .SelectMany(g => g)
    .ToArray();

var duplicateCandidatesWithEpisodes = new List<DuplicatePodcastItem>();

foreach (var candidate in duplicateCandidates)
{
    DateTime? oldestRelease = null;

    await foreach (var episode in episodeRepository.GetByPodcastId(candidate.Id))
    {
        if (!oldestRelease.HasValue || episode.Release < oldestRelease.Value)
        {
            oldestRelease = episode.Release;
        }
    }

    if (!oldestRelease.HasValue)
    {
        continue;
    }

    duplicateCandidatesWithEpisodes.Add(candidate with
    {
        OldestEpisodeRelease = oldestRelease.Value
    });
}

var duplicates = duplicateCandidatesWithEpisodes
    .GroupBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
    .Where(g => g.Count() > 1)
    .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
    .Select(g =>
    {
        var ordered = g
            .OrderBy(p => p.OldestEpisodeRelease)
            .ThenBy(p => p.Id)
            .ToArray();

        return new DuplicatePodcastGroup(
            g.Key,
            ordered[0].Id,
            ordered);
    })
    .ToArray();

Console.WriteLine($"Duplicate podcast names with episodes: {duplicates.Length}");

var json = JsonSerializer.Serialize(duplicates, new JsonSerializerOptions
{
    WriteIndented = true,
});

Console.WriteLine(json);

return;

static List<string> ValidateCanonical(Podcast canonicalPodcast, IReadOnlyCollection<Podcast> detachedPodcasts)
{
    var errors = new List<string>();
    var properties = typeof(Podcast)
        .GetProperties(BindingFlags.Public | BindingFlags.Instance)
        .Where(p => p.CanRead)
        .Where(p => p.Name is not nameof(Podcast.Id) and not nameof(Podcast.Timestamp) and not nameof(Podcast.FileKey));

    foreach (var detachedPodcast in detachedPodcasts)
    {
        foreach (var property in properties)
        {
            var detachedValue = property.GetValue(detachedPodcast);
            if (!HasMeaningfulValue(detachedValue))
            {
                continue;
            }

            var canonicalValue = property.GetValue(canonicalPodcast);
            if (!HasMeaningfulValue(canonicalValue))
            {
                errors.Add($"Detach podcast {detachedPodcast.Id}: canonical podcast missing value for '{property.Name}'.");
                continue;
            }

            if (!ValuesCompatible(property.Name, detachedValue, canonicalValue))
            {
                errors.Add($"Detach podcast {detachedPodcast.Id}: canonical value for '{property.Name}' is incompatible.");
            }
        }
    }

    return errors;
}

static bool HasMeaningfulValue(object? value)
{
    return value switch
    {
        null => false,
        string s => !string.IsNullOrWhiteSpace(s),
        Array a => a.Length > 0,
        _ => true
    };
}

static bool ValuesCompatible(string propertyName, object detachedValue, object canonicalValue)
{
    if (propertyName == nameof(Podcast.Name))
    {
        return string.Equals((string)detachedValue, (string)canonicalValue, StringComparison.OrdinalIgnoreCase);
    }

    if (detachedValue is string detachedString && canonicalValue is string canonicalString)
    {
        return string.Equals(detachedString, canonicalString, StringComparison.Ordinal);
    }

    if (detachedValue is string[] detachedStrings && canonicalValue is string[] canonicalStrings)
    {
        var canonicalSet = new HashSet<string>(canonicalStrings, StringComparer.OrdinalIgnoreCase);
        return detachedStrings.All(canonicalSet.Contains);
    }

    return Equals(detachedValue, canonicalValue);
}

static HashSet<Guid> ParseGuids(string? text)
{
    if (string.IsNullOrWhiteSpace(text))
    {
        return [];
    }

    var result = new HashSet<Guid>();
    var parts = text.Split([',', ';', '|', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    foreach (var part in parts)
    {
        if (!Guid.TryParse(part, out var id))
        {
            throw new InvalidOperationException($"Invalid guid in detachPodcastIds: '{part}'");
        }

        result.Add(id);
    }

    return result;
}

string GetBasePath()
{
    using var processModule = Process.GetCurrentProcess().MainModule;
    return Path.GetDirectoryName(processModule?.FileName) ?? throw new InvalidOperationException();
}

public sealed record DuplicatePodcastItem(
    string Name,
    Guid Id,
    long? AppleId,
    string SpotifyId,
    string YouTubeId,
    DateTime? OldestEpisodeRelease);

public sealed record DuplicatePodcastGroup(
    string Name,
    Guid CanonicalPodcastId,
    IReadOnlyList<DuplicatePodcastItem> Podcasts);

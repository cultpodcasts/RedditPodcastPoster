using System.Text.Json;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Spectre.Console;
using static RedditPodcastPoster.Models.Cosmos.FileKeyFactory;
using RedditPodcastPoster.Models.Cosmos;
using RedditPodcastPoster.Models.TitleCasing;
using RedditPodcastPoster.Persistence.Abstractions.Providers;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.Persistence.Writers;
using RedditPodcastPoster.Text.KnownTerms;

namespace CosmosDbDownloader;

public class CosmosDbDownloader(
    ISafeFileEntityWriter fileWriter,
    IFileRepository fileRepository,
    IPodcastRepository podcastRepository,
    IEpisodeRepository episodeRepository,
    ISubjectRepository subjectRepository,
    ILookupRepository lookupRepository,
    ILanguageTitleCasingRulesRepository titleCasingRulesRepository,
    IDiscoveryResultsRepository discoveryResultsRepository,
    IPushSubscriptionRepository pushSubscriptionRepository,
    IJsonSerializerOptionsProvider jsonSerializerOptionsProvider,
    ILogger<CosmosDbDownloader> logger)
{
    private const string FileExtension = ".json";

    /// <summary>
    /// Bounded concurrency for serialize + disk write. Cosmos feed iteration stays single-threaded
    /// per container (FeedIterator is not safe for concurrent MoveNext).
    /// </summary>
    private static readonly int WriteParallelism = Math.Clamp(Environment.ProcessorCount * 2, 4, 16);

    private readonly JsonSerializerOptions _jsonOptions = jsonSerializerOptionsProvider.GetJsonSerializerOptions();
    private bool _overwrite;

    public async Task Run(CosmosDbDownloaderRequest request)
    {
        var selection = DownloadContainerSelection.FromRequest(request);
        _overwrite = request.Overwrite;
        AnsiConsole.MarkupLine(
            $"[grey]Write parallelism:[/] {WriteParallelism} (Cosmos reads stay sequential per container)");
        AnsiConsole.MarkupLine(
            $"[grey]Containers:[/] {string.Join(", ", selection.EnabledNames)}");
        AnsiConsole.MarkupLine(
            $"[grey]Overwrite:[/] {(_overwrite ? "yes" : "no (existing files are errors)")}");

        await AnsiConsole.Progress()
            .AutoClear(false)
            .HideCompleted(false)
            .Columns(
                new TaskDescriptionColumn(),
                new ProgressBarColumn(),
                new PercentageColumn(),
                new RemainingTimeColumn(),
                new SpinnerColumn())
            .StartAsync(async ctx =>
            {
                await TestFileKeysPerContainer(ctx, selection);

                var downloads = new List<Task>();
                if (selection.Podcasts)
                {
                    downloads.Add(DownloadPodcasts(ctx.AddTask("Podcasts")));
                }

                if (selection.Episodes)
                {
                    downloads.Add(DownloadEpisodes(ctx.AddTask("Episodes")));
                }

                if (selection.LookUps)
                {
                    downloads.Add(DownloadLookUps(ctx.AddTask("LookUps")));
                }

                if (selection.TitleCasing)
                {
                    downloads.Add(DownloadTitleCasingRules(ctx.AddTask("Title-casing rules")));
                }

                if (selection.Subjects)
                {
                    downloads.Add(DownloadSubjects(ctx.AddTask("Subjects")));
                }

                if (selection.Discovery)
                {
                    downloads.Add(DownloadDiscoveryResultsDocuments(ctx.AddTask("Discovery results")));
                }

                if (selection.PushSubscriptions)
                {
                    downloads.Add(DownloadPushSubscriptions(ctx.AddTask("Push subscriptions")));
                }

                await Task.WhenAll(downloads);
            });

        AnsiConsole.MarkupLine("[green]Finished downloading selected containers.[/]");
    }

    private async Task TestFileKeysPerContainer(ProgressContext ctx, DownloadContainerSelection selection)
    {
        var checks = new List<Task>();

        if (selection.Podcasts)
        {
            checks.Add(ValidateFileKeys(
                podcastRepository.GetAll().Select(p => p.FileKey),
                "Podcasts",
                ctx.AddTask("File-key check: Podcasts", maxValue: 1)));
        }

        if (selection.Subjects)
        {
            checks.Add(ValidateFileKeys(
                subjectRepository.GetAll().Select(s => s.FileKey),
                "Subjects",
                ctx.AddTask("File-key check: Subjects", maxValue: 1)));
        }

        if (selection.Discovery)
        {
            checks.Add(ValidateFileKeys(
                discoveryResultsRepository.GetAll().Select(d => d.FileKey),
                "Discovery Results",
                ctx.AddTask("File-key check: Discovery", maxValue: 1)));
        }

        if (selection.PushSubscriptions)
        {
            checks.Add(ValidateFileKeys(
                pushSubscriptionRepository.GetAll().Select(p => p.FileKey),
                "Push Subscriptions",
                ctx.AddTask("File-key check: Push subscriptions", maxValue: 1)));
        }

        if (selection.TitleCasing)
        {
            checks.Add(ValidateFileKeys(
                titleCasingRulesRepository.GetAll().Select(t => t.FileKey),
                "TitleCasingRules",
                ctx.AddTask("File-key check: Title-casing", maxValue: 1)));
        }

        if (selection.LookUps)
        {
            checks.Add(ValidateLookUpFileKeys(ctx.AddTask("File-key check: LookUps", maxValue: 1)));
        }

        if (checks.Count > 0)
        {
            await Task.WhenAll(checks);
        }
    }

    private async Task ValidateLookUpFileKeys(ProgressTask progress)
    {
        var keys = new List<string>();
        foreach (var item in await LoadLookUpDocuments())
        {
            keys.Add(item.FileKey);
        }

        ValidateFileKeyList(keys, "LookUps");
        progress.Increment(1);
        progress.StopTask();
    }

    private static async Task ValidateFileKeys(
        IAsyncEnumerable<string> allFileKeys,
        string containerName,
        ProgressTask progress)
    {
        var keys = new List<string>();
        await foreach (var fileKey in allFileKeys)
        {
            keys.Add(fileKey);
        }

        ValidateFileKeyList(keys, containerName);
        progress.Increment(1);
        progress.StopTask();
    }

    private static void ValidateFileKeyList(IReadOnlyList<string> fileKeys, string containerName)
    {
        var invalid = new List<string>();
        var distinct = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var duplicate = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var fileKey in fileKeys)
        {
            if (!distinct.Add(fileKey ?? string.Empty))
            {
                duplicate.Add(fileKey ?? string.Empty);
            }

            if (!IsValidWindowsFileName(fileKey))
            {
                invalid.Add(fileKey ?? "(null/empty)");
            }
        }

        if (duplicate.Count > 0 || invalid.Count > 0)
        {
            var parts = new List<string>();
            if (duplicate.Count > 0)
            {
                parts.Add($"duplicate file-keys: '{string.Join("', '", duplicate)}'");
            }

            if (invalid.Count > 0)
            {
                parts.Add(
                    $"invalid Windows file-keys (must not contain {FormatInvalidFileNameChars()}): '{string.Join("', '", invalid)}'");
            }

            throw new InvalidOperationException(
                $"File-key validation failed for {containerName} — {string.Join("; ", parts)}. Fix Cosmos data before downloading.");
        }
    }

    private static bool IsValidWindowsFileName(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return false;
        }

        if (fileName is "." or "..")
        {
            return false;
        }

        return fileName.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;
    }

    private static string FormatInvalidFileNameChars()
    {
        var chars = Path.GetInvalidFileNameChars()
            .Where(c => !char.IsControl(c))
            .Select(c => c.ToString())
            .ToArray();
        return string.Join(' ', chars);
    }

    private async Task DownloadPodcasts(ProgressTask progress)
    {
        var count = await podcastRepository.Count();
        progress.MaxValue = Math.Max(count, 1);
        Directory.CreateDirectory("podcast");

        if (count == 0)
        {
            progress.Increment(1);
            progress.StopTask();
            return;
        }

        await WriteAllParallelAsync(
            podcastRepository.GetAll(),
            podcast => WriteJson("podcast", podcast.FileKey, podcast),
            progress);
        progress.StopTask();
    }

    private async Task DownloadEpisodes(ProgressTask progress)
    {
        var count = await episodeRepository.Count();
        progress.MaxValue = Math.Max(count, 1);
        Directory.CreateDirectory("episode");

        if (count == 0)
        {
            progress.Increment(1);
            progress.StopTask();
            return;
        }

        await WriteAllParallelAsync(
            episodeRepository.GetAll(),
            episode => WriteJson("episode", episode.Id.ToString(), episode),
            progress);
        progress.StopTask();
    }

    private async Task DownloadSubjects(ProgressTask progress)
    {
        var count = await subjectRepository.Count();
        progress.MaxValue = Math.Max(count, 1);

        if (count == 0)
        {
            progress.Increment(1);
            progress.StopTask();
            return;
        }

        // File-key backfill may Save() to Cosmos — keep enumeration + save sequential.
        await foreach (var subject in subjectRepository.GetAll())
        {
            if (string.IsNullOrWhiteSpace(subject.FileKey))
            {
                logger.LogInformation("Subject with id '{SubjectId}' missing a file-key.", subject.Id);
                subject.FileKey = GetFileKey(subject.Name);
                await subjectRepository.Save(subject);
            }

            await WriteEntityAsync(subject);
            progress.Increment(1);
        }

        progress.StopTask();
    }

    private async Task DownloadDiscoveryResultsDocuments(ProgressTask progress)
    {
        var count = await discoveryResultsRepository.Count();
        progress.MaxValue = Math.Max(count, 1);

        if (count == 0)
        {
            progress.Increment(1);
            progress.StopTask();
            return;
        }

        await foreach (var document in discoveryResultsRepository.GetAll())
        {
            if (string.IsNullOrWhiteSpace(document.FileKey))
            {
                logger.LogInformation(
                    "Discovery-Results-Document with id '{Guid}' missing a file-key.", document.Id);
                document.FileKey = GetFileKey("dr " + document.Id);
                await discoveryResultsRepository.Save(document);
            }

            await WriteEntityAsync(document);
            progress.Increment(1);
        }

        progress.StopTask();
    }

    private async Task DownloadPushSubscriptions(ProgressTask progress)
    {
        var count = await pushSubscriptionRepository.Count();
        progress.MaxValue = Math.Max(count, 1);

        if (count == 0)
        {
            progress.Increment(1);
            progress.StopTask();
            return;
        }

        await foreach (var subscription in pushSubscriptionRepository.GetAll())
        {
            if (string.IsNullOrWhiteSpace(subscription.FileKey))
            {
                logger.LogInformation(
                    "Push-Subscription-Document with id '{Guid}' missing a file-key.", subscription.Id);
                subscription.FileKey = GetFileKey("ps_" + subscription.Id);
                await pushSubscriptionRepository.Save(subscription);
            }

            await WriteEntityAsync(subscription);
            progress.Increment(1);
        }

        progress.StopTask();
    }

    /// <summary>
    /// Downloads known LookUps document types via typed repository getters into <c>lookups/</c>.
    /// Title-casing known terms live in the TitleCasingRules container (<c>titlecasing/</c>).
    /// </summary>
    private async Task DownloadLookUps(ProgressTask progress)
    {
        Directory.CreateDirectory("lookups");
        var items = await LoadLookUpDocuments();

        progress.MaxValue = Math.Max(items.Count, 1);
        if (items.Count == 0)
        {
            progress.Increment(1);
            progress.StopTask();
            return;
        }

        await Parallel.ForEachAsync(
            items,
            new ParallelOptions { MaxDegreeOfParallelism = WriteParallelism },
            async (item, _) =>
            {
                // Serialize the runtime type — CosmoSelector would drop derived members.
                await WriteJson(item.GetType(), "lookups", item.FileKey, item);
                progress.Increment(1);
            });

        progress.StopTask();
    }

    private async Task<List<CosmosSelector>> LoadLookUpDocuments()
    {
        CosmosSelector?[] candidates =
        [
            await lookupRepository.GetEliminationTerms(),
            await lookupRepository.GetDiscoveryScheduleConfig(),
            await lookupRepository.GetSupportedLanguagesConfig(),
            await lookupRepository.GetKnownTerms<KnownTerms>(),
            await lookupRepository.GetHomePageCache(),
            await lookupRepository.GetYouTubeQuotaReport(),
            await lookupRepository.GetYouTubeIndexerKeyState(),
            await lookupRepository.GetYouTubeQuotaUsageState(),
        ];

        return candidates.OfType<CosmosSelector>().ToList();
    }

    private async Task DownloadTitleCasingRules(ProgressTask progress)
    {
        Directory.CreateDirectory("titlecasing");
        var documents = new List<TitleCasingRulesDocument>();
        await foreach (var document in titleCasingRulesRepository.GetAll())
        {
            documents.Add(document);
        }

        progress.MaxValue = Math.Max(documents.Count, 1);

        if (documents.Count == 0)
        {
            progress.Increment(1);
            progress.StopTask();
            return;
        }

        // Small container — write sequentially so a bad FileKey (e.g. Universal "*") fails loudly
        // instead of looking like a hung parallel progress bar at N-1/N.
        foreach (var document in documents)
        {
            logger.LogInformation(
                "Title-casing rules: language={Language} file={FileName}",
                document.Language,
                ToSafeFileName(document.FileKey));
            await WriteJson("titlecasing", document.FileKey, document);
            progress.Increment(1);
        }

        progress.StopTask();
    }

    /// <summary>
    /// Single Cosmose reader + N disk writers via a bounded channel. Avoids concurrent
    /// FeedIterator.MoveNext (unsafe) while overlapping serialize/write with the next page fetch.
    /// </summary>
    private static async Task WriteAllParallelAsync<T>(
        IAsyncEnumerable<T> source,
        Func<T, Task> writeAsync,
        ProgressTask progress)
    {
        var channel = Channel.CreateBounded<T>(new BoundedChannelOptions(WriteParallelism * 2)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = false,
            SingleWriter = true
        });

        var writers = Enumerable.Range(0, WriteParallelism).Select(async _ =>
        {
            await foreach (var item in channel.Reader.ReadAllAsync())
            {
                await writeAsync(item);
                progress.Increment(1);
            }
        }).ToArray();

        try
        {
            await foreach (var item in source)
            {
                await channel.Writer.WriteAsync(item);
            }

            channel.Writer.Complete();
            await Task.WhenAll(writers);
        }
        catch (Exception)
        {
            channel.Writer.TryComplete();
            try
            {
                await Task.WhenAll(writers);
            }
            catch
            {
                // Prefer the producer/write failure that started the shutdown.
            }

            throw;
        }
    }

    /// <summary>
    /// Windows rejects <c>*</c> and other invalid filename chars. Universal title-casing
    /// rules use language <c>*</c>, so FileKey <c>TitleCasingRules-*</c> must be sanitised
    /// or the write hangs the progress bar at (n-1)/n after en/es succeed.
    /// </summary>
    private static string ToSafeFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new InvalidOperationException("Cannot write a file with an empty name.");
        }

        var safe = fileName.Replace(
            TitleCasingRulesDocument.UniversalLanguageKey,
            "universal",
            StringComparison.Ordinal);

        foreach (var c in Path.GetInvalidFileNameChars())
        {
            safe = safe.Replace(c, '_');
        }

        if (string.IsNullOrWhiteSpace(safe) || safe is "." or "..")
        {
            throw new InvalidOperationException(
                $"File name '{fileName}' sanitised to an invalid Windows name '{safe}'.");
        }

        return safe;
    }

    private async Task WriteEntityAsync<T>(T data) where T : RedditPodcastPoster.Models.Cosmos.CosmosSelector
    {
        var path = fileRepository.GetFilePath(data);
        EnsureWritable(path, data.FileKey);
        if (_overwrite)
        {
            await fileRepository.Write(data);
            return;
        }

        await fileWriter.Write(data);
    }

    private void EnsureWritable(string path, string displayName)
    {
        if (!File.Exists(path))
        {
            return;
        }

        if (!_overwrite)
        {
            throw new InvalidOperationException(
                $"File '{path}' already exists when writing item '{displayName}'. Re-run with --overwrite to replace it.");
        }

        File.Delete(path);
    }

    private async Task WriteJson<T>(string folder, string fileName, T item) where T : notnull
    {
        await WriteJson(typeof(T), folder, fileName, item);
    }

    private async Task WriteJson(Type runtimeType, string folder, string fileName, object item)
    {
        var safeName = ToSafeFileName(fileName);
        var path = Path.Combine(folder, $"{safeName}{FileExtension}");
        Directory.CreateDirectory(folder);
        EnsureWritable(path, safeName);

        var json = JsonSerializer.Serialize(item, runtimeType, _jsonOptions);
        await File.WriteAllTextAsync(path, json);
    }
}

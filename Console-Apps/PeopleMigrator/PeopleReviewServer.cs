using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace PeopleMigrator;

internal static class PeopleReviewServer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public static async Task RunAsync(IHost host, PeopleMigrationRequest request, CancellationToken cancellationToken = default)
    {
        var seedPath = Path.GetFullPath(
            string.IsNullOrWhiteSpace(request.SeedPath) ? "people-seed.json" : request.SeedPath);
        if (!File.Exists(seedPath))
        {
            throw new FileNotFoundException($"Seed file not found: {seedPath}");
        }

        var reviewerRoot = Path.Combine(AppContext.BaseDirectory, "reviewer");
        if (!Directory.Exists(reviewerRoot))
        {
            reviewerRoot = Path.Combine(Directory.GetCurrentDirectory(), "reviewer");
        }

        var builder = WebApplication.CreateBuilder();
        builder.Services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            options.SerializerOptions.PropertyNameCaseInsensitive = true;
        });
        builder.Services.AddSingleton(new ReviewSeedStore(seedPath));
        builder.Services.AddSingleton(host.Services.GetRequiredService<IPersonDisplayNameResolver>());
        builder.Logging.ClearProviders();
        builder.Logging.AddConsole();

        var app = builder.Build();
        app.UseDefaultFiles(new DefaultFilesOptions
        {
            FileProvider = new PhysicalFileProvider(reviewerRoot),
            RequestPath = string.Empty
        });
        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new PhysicalFileProvider(reviewerRoot),
            RequestPath = string.Empty
        });

        app.MapGet("/api/seed", async (ReviewSeedStore store, CancellationToken ct) =>
        {
            var document = await store.LoadAsync(ct);
            return Results.Json(document, JsonOptions);
        });

        app.MapPost("/api/episode-urls", async (ReviewSeedStore store, EpisodeUrlRequest request, CancellationToken ct) =>
        {
            var document = await store.LoadAsync(ct);
            var ids = (request.Ids ?? [])
                .Select(id => Guid.TryParse(id, out var parsed) ? parsed : (Guid?)null)
                .Where(id => id.HasValue)
                .Select(id => id!.Value);

            var resolver = new EpisodeUrlResolver(document.SourceBackupPath);
            return Results.Json(resolver.ResolveMany(ids), JsonOptions);
        });

        app.MapPut("/api/seed", async (ReviewSeedStore store, PeopleSeedJsonWriter.PeopleSeedDocument document, CancellationToken ct) =>
        {
            await store.SaveAsync(document, ct);
            return Results.Ok(new { saved = true, path = store.SeedPath });
        });

        app.MapPut("/api/people/{index:int}", async (int index, ReviewSeedStore store, PeopleSeedJsonWriter.PeopleSeedEntry entry, CancellationToken ct) =>
        {
            var document = await store.LoadAsync(ct);
            if (index < 0 || index >= document.People.Count)
            {
                return Results.NotFound();
            }

            document.People[index] = entry;
            await store.SaveAsync(document, ct);
            return Results.Ok(entry);
        });

        app.MapPost("/api/swap-canonical", (SwapCanonicalRequest request) =>
        {
            try
            {
                var result = CanonicalNamePromoter.SwapCanonicalWithAlias(
                    request.CanonicalName ?? string.Empty,
                    request.Aliases,
                    request.Alias ?? string.Empty,
                    request.TwitterHandle,
                    request.BlueskyHandle);

                return Results.Json(new
                {
                    name = result.CanonicalName,
                    aliases = result.Aliases
                }, JsonOptions);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
        });

        app.MapPost("/api/people/{index:int}/refresh-profile", async (
            int index,
            ReviewSeedStore store,
            IPersonDisplayNameResolver resolver,
            CancellationToken ct) =>
        {
            var document = await store.LoadAsync(ct);
            if (index < 0 || index >= document.People.Count)
            {
                return Results.NotFound();
            }

            var person = document.People[index];
            var resolution = await resolver.ResolveDisplayNameAsync(
                person.TwitterHandle,
                person.BlueskyHandle,
                ct);

            return Results.Json(new
            {
                chosenName = resolution.ChosenName,
                twitterName = resolution.TwitterName,
                blueskyName = resolution.BlueskyName,
                chosenSource = resolution.ChosenSource
            }, JsonOptions);
        });

        var url = $"http://127.0.0.1:{request.Port}";
        Console.WriteLine("People seed reviewer running at {0}", url);
        Console.WriteLine("Seed file: {0}", seedPath);
        Console.WriteLine("Press Ctrl+C to stop.");

        await app.RunAsync(url);
    }

    private sealed class EpisodeUrlRequest
    {
        public string[]? Ids { get; set; }
    }

    private sealed class SwapCanonicalRequest
    {
        public string? CanonicalName { get; set; }

        public string? Alias { get; set; }

        public string[]? Aliases { get; set; }

        public string? TwitterHandle { get; set; }

        public string? BlueskyHandle { get; set; }
    }

    internal sealed class ReviewSeedStore(string seedPath)
    {
        private readonly SemaphoreSlim _gate = new(1, 1);

        public string SeedPath { get; } = seedPath;

        public async Task<PeopleSeedJsonWriter.PeopleSeedDocument> LoadAsync(CancellationToken cancellationToken)
        {
            await _gate.WaitAsync(cancellationToken);
            try
            {
                await using var stream = File.OpenRead(SeedPath);
                return await PeopleSeedJsonWriter.DeserializeDocumentAsync(stream, cancellationToken)
                    ?? throw new InvalidDataException($"Seed file is empty or invalid: {SeedPath}");
            }
            finally
            {
                _gate.Release();
            }
        }

        public async Task SaveAsync(PeopleSeedJsonWriter.PeopleSeedDocument document, CancellationToken cancellationToken)
        {
            await _gate.WaitAsync(cancellationToken);
            try
            {
                document.GeneratedAt = DateTimeOffset.UtcNow;
                var directory = Path.GetDirectoryName(SeedPath);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                await using var stream = File.Create(SeedPath);
                await JsonSerializer.SerializeAsync(stream, document, JsonOptions, cancellationToken);
            }
            finally
            {
                _gate.Release();
            }
        }
    }
}

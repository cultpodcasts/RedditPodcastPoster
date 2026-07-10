using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;

namespace PeopleReviewer;

internal static class PeopleReviewServer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true
    };

    public static async Task RunAsync(PeopleReviewRequest request, CancellationToken cancellationToken = default)
    {
        var seedPath = ResolveSeedPath(request.SeedPath);
        if (!File.Exists(seedPath))
        {
            throw new FileNotFoundException($"Seed file not found: {seedPath}");
        }

        var reviewerRoot = Path.Combine(AppContext.BaseDirectory, "reviewer");
        if (!Directory.Exists(reviewerRoot))
        {
            reviewerRoot = Path.Combine(Directory.GetCurrentDirectory(), "reviewer");
        }

        if (!Directory.Exists(reviewerRoot))
        {
            throw new DirectoryNotFoundException($"Reviewer static files not found under {reviewerRoot}");
        }

        var builder = WebApplication.CreateBuilder();
        builder.Services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            options.SerializerOptions.PropertyNameCaseInsensitive = true;
            options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        });
        builder.Services.AddSingleton(new ReviewSeedStore(seedPath));
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

        app.MapPut("/api/seed", async (ReviewSeedStore store, PeopleSeedDocument document, CancellationToken ct) =>
        {
            await store.SaveAsync(document, ct);
            return Results.Ok(new { saved = true, path = store.SeedPath });
        });

        app.MapPut("/api/people/{index:int}", async (int index, ReviewSeedStore store, PeopleSeedEntry entry, CancellationToken ct) =>
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

        var url = $"http://127.0.0.1:{request.Port}";
        Console.WriteLine("People seed reviewer running at {0}", url);
        Console.WriteLine("Seed file: {0}", seedPath);
        Console.WriteLine("JSON edits only — no Cosmos or episode writes.");
        Console.WriteLine("Press Ctrl+C to stop.");

        await app.RunAsync(url);
    }

    internal static string ResolveSeedPath(string? seedPath)
    {
        if (!string.IsNullOrWhiteSpace(seedPath))
        {
            return Path.GetFullPath(seedPath);
        }

        var sample = Path.Combine(AppContext.BaseDirectory, "sample-people-seed.json");
        if (File.Exists(sample))
        {
            return Path.GetFullPath(sample);
        }

        return Path.GetFullPath("people-seed.json");
    }

    internal sealed class ReviewSeedStore(string seedPath)
    {
        private readonly SemaphoreSlim _gate = new(1, 1);

        public string SeedPath { get; } = seedPath;

        public async Task<PeopleSeedDocument> LoadAsync(CancellationToken cancellationToken)
        {
            await _gate.WaitAsync(cancellationToken);
            try
            {
                await using var stream = File.OpenRead(SeedPath);
                return await JsonSerializer.DeserializeAsync<PeopleSeedDocument>(stream, JsonOptions, cancellationToken)
                    ?? throw new InvalidDataException($"Seed file is empty or invalid: {SeedPath}");
            }
            finally
            {
                _gate.Release();
            }
        }

        public async Task SaveAsync(PeopleSeedDocument document, CancellationToken cancellationToken)
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

using System.Linq.Expressions;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models.Cosmos;
using RedditPodcastPoster.Persistence.Abstractions.FileKeys;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;

namespace RedditPodcastPoster.Persistence.FileKeys;

/// <inheritdoc cref="IFileKeyAllocator"/>
/// <remarks>
/// Taken keys are read per allocation from the kind's own container, then claimed in <c>FileKeyClaims</c>
/// (<see cref="IFileKeyClaimStore"/>). A 409 from another owner moves on to the next <c>_N</c> suffix.
/// The same owner's retry keeps the key. A second scope that only sees that uncommitted claim does not release
/// it. Saving the item acknowledges the claim. Disposing this allocator releases claims it created that were
/// never saved, when it still holds the lease. Keys this allocator generates for different kinds cannot
/// clash (TV, news and film keys carry reserved prefixes and a generated podcast slug cannot contain a hyphen),
/// but a key set by other means (hand-edited, or older data) can. The public-database publisher's
/// <c>PublicFileKeyGuard</c> remains the backstop for those.
/// </remarks>
public sealed class FileKeyAllocator(
    IPodcastRepository podcastRepository,
    ITvShowRepository tvShowRepository,
    INewsOrganisationRepository newsOrganisationRepository,
    IFilmRepository filmRepository,
    IFileKeyClaimStore fileKeyClaims,
    ILogger<FileKeyAllocator> logger) : IFileKeyAllocator
{
    private const int MaxSuffixes = 1000;
    private readonly Guid _scopeId = Guid.NewGuid();
    public async Task<string> Allocate(
        FileKeyKind kind,
        string name,
        Guid id,
        string? fallbackSlug = null)
    {
        var candidate = FileKeyFactory.GetFileKey(kind, name ?? string.Empty, id, fallbackSlug);
        return await MakeUnique(kind, candidate, id);
    }

    public async Task<string> AllocateForRename(FileKeyKind kind, string name, Guid id, string? currentKey)
    {
        var prefix = FileKeyFactory.PrefixFor(kind);
        var slug = FileKeyFactory.GetFileKey(name ?? string.Empty);
        if (!FileKeyFactory.IsUsableSlug(slug) && FileKeyFactory.IsUsableFileKey(currentKey, prefix))
        {
            return currentKey!;
        }

        var candidate = FileKeyFactory.GetFileKey(kind, name ?? string.Empty, id);
        // Ordinal-equal only: ignore-case match (wrong-case prefix / mixed slug) must go through MakeUnique
        // so a taken lower-case key gets a _2 suffix instead of a bare colliding candidate.
        if (currentKey is not null &&
            string.Equals(candidate, currentKey, StringComparison.Ordinal))
        {
            return currentKey;
        }

        return await MakeUnique(kind, candidate, id);
    }

    public async Task<string> RepairIfUnusable(FileKeyKind kind, string name, Guid id, string? currentKey) =>
        FileKeyFactory.IsUsableFileKey(currentKey, FileKeyFactory.PrefixFor(kind))
            ? currentKey!
            : await Allocate(kind, name, id);

    public Task Release(string fileKey, Guid ownerId) => fileKeyClaims.Delete(fileKey, ownerId);

    public async ValueTask DisposeAsync()
    {
        try
        {
            await fileKeyClaims.ReleaseUnacknowledged(_scopeId);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "{method}: Failed to release unacknowledged file-key claims.", nameof(DisposeAsync));
        }
    }

    private async Task<string> MakeUnique(FileKeyKind kind, string candidate, Guid id)
    {
        var taken = await TakenKeysStartingWith(kind, candidate, id);
        for (var attempt = 0; attempt < MaxSuffixes; attempt++)
        {
            var next = FileKeyFactory.MakeUnique(candidate, taken.Contains);
            var claim = await fileKeyClaims.TryClaim(next, id, _scopeId);
            if (claim != FileKeyClaimResult.HeldByOther)
            {
                return next;
            }

            taken.Add(next);
        }

        throw new InvalidOperationException($"Could not claim a file key starting '{candidate}'.");
    }

    private Task<HashSet<string>> TakenKeysStartingWith(FileKeyKind kind, string candidate, Guid id) =>
        kind switch
        {
            FileKeyKind.Podcast => Read(podcastRepository, candidate, id),
            FileKeyKind.TvShow => Read(tvShowRepository, candidate, id),
            FileKeyKind.NewsOrganisation => Read(newsOrganisationRepository, candidate, id),
            FileKeyKind.Film => Read(filmRepository, candidate, id),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
        };

    private static async Task<HashSet<string>> Read<TEntity>(
        IFilterableRepository<TEntity> repository,
        string candidate,
        Guid id) where TEntity : CosmosSelector, IPublicEntity
    {
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await foreach (var key in repository.GetAllBy(TakenKeySelector<TEntity>(candidate, id), x => x.FileKey))
        {
            keys.Add(key);
        }

        return keys;
    }

    /// <summary>
    /// Cosmos filter for keys already taken by other documents of the kind. Constrained to <see cref="IPublicEntity"/>
    /// because only the public kinds (podcast, TV show, news organisation, film) get allocated keys. The SQL name comes
    /// from the camelCase naming policy (or the concrete <c>[JsonPropertyName("fileKey")]</c>, which pins it
    /// independently of that policy). Must translate to <c>c.fileKey</c> (pinned by
    /// <c>FileKeyAllocatorCosmosFilterTranslationRules</c>).
    /// </summary>
    public static Expression<Func<TEntity, bool>> TakenKeySelector<TEntity>(string candidate, Guid id)
        where TEntity : CosmosSelector, IPublicEntity =>
        x => x.FileKey != null && x.FileKey.StartsWith(candidate, StringComparison.OrdinalIgnoreCase) && x.Id != id;
}

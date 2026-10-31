using System.Linq.Expressions;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.Cosmos;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;

namespace RedditPodcastPoster.Persistence.Abstractions.FileKeys;

/// <inheritdoc cref="IFileKeyAllocator"/>
/// <remarks>
/// Taken keys are read per allocation from the kind's own container only. Keys this allocator generates for different
/// kinds cannot clash (TV, news and film keys carry reserved prefixes and a generated podcast slug cannot contain a
/// hyphen), but a key set by other means (hand-edited, or older data) can; nothing is claimed between allocation and
/// save, so two concurrent creates with the same name can also get the same key. The public-database publisher's
/// <c>PublicFileKeyGuard</c> is the backstop for both (cross-process claim: issue #1165).
/// </remarks>
public sealed class FileKeyAllocator(
    IPodcastRepository podcastRepository,
    ITvShowRepository tvShowRepository,
    INewsOrganisationRepository newsOrganisationRepository,
    IFilmRepository filmRepository) : IFileKeyAllocator
{
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
        if (currentKey is not null && string.Equals(candidate, currentKey, StringComparison.OrdinalIgnoreCase))
        {
            return currentKey;
        }

        return await MakeUnique(kind, candidate, id);
    }

    public async Task<string> RepairIfUnusable(FileKeyKind kind, string name, Guid id, string? currentKey) =>
        FileKeyFactory.IsUsableFileKey(currentKey, FileKeyFactory.PrefixFor(kind))
            ? currentKey!
            : await Allocate(kind, name, id);

    private async Task<string> MakeUnique(FileKeyKind kind, string candidate, Guid id)
    {
        var taken = await TakenKeysStartingWith(kind, candidate, id);
        return FileKeyFactory.MakeUnique(candidate, taken.Contains);
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
        Guid id) where TEntity : Publisher
    {
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await foreach (var key in repository.GetAllBy(TakenKeySelector<TEntity>(candidate, id), x => x.FileKey))
        {
            keys.Add(key);
        }

        return keys;
    }

    /// <summary>
    /// Cosmos filter for keys already taken by other documents of the kind. Constrained to <see cref="Publisher"/>
    /// (not <see cref="IFileKeyed"/>) so <c>x.FileKey</c> binds to <see cref="Publisher.FileKey"/>, whose
    /// <c>[JsonPropertyName("fileKey")]</c> the Cosmos LINQ provider reads; an interface member carries no JSON name.
    /// Must translate to <c>c.fileKey</c> (pinned by <c>FileKeyAllocatorCosmosFilterTranslationRules</c>).
    /// </summary>
    public static Expression<Func<TEntity, bool>> TakenKeySelector<TEntity>(string candidate, Guid id)
        where TEntity : Publisher =>
        x => x.FileKey != null && x.FileKey.StartsWith(candidate, StringComparison.OrdinalIgnoreCase) && x.Id != id;
}

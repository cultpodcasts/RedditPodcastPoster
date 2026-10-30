using System.Linq.Expressions;
using RedditPodcastPoster.Models.Cosmos;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;

namespace RedditPodcastPoster.Persistence.Abstractions.FileKeys;

/// <inheritdoc cref="IFileKeyAllocator"/>
/// <remarks>
/// Taken keys are read per allocation from the kind's own container (keys of different kinds never clash because
/// TV, news and film keys carry reserved prefixes and podcast slugs cannot contain a hyphen). Two concurrent creates
/// with the same name can still race; the public-database publisher's duplicate check remains the backstop.
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
        string? fallbackSlug = null,
        Func<string, bool>? alsoTaken = null)
    {
        var prefix = Prefix(kind);
        var candidate = prefix + FileKeyFactory.GetFileKey(name ?? string.Empty, id, fallbackSlug);
        return await MakeUnique(kind, candidate, id, alsoTaken);
    }

    public async Task<string> AllocateForRename(FileKeyKind kind, string name, Guid id, string? currentKey)
    {
        var prefix = Prefix(kind);
        var slug = FileKeyFactory.GetFileKey(name ?? string.Empty);
        if (!FileKeyFactory.IsUsableSlug(slug) && FileKeyFactory.IsUsableFileKey(currentKey, prefix))
        {
            return currentKey!;
        }

        var candidate = prefix + FileKeyFactory.GetFileKey(name ?? string.Empty, id);
        if (currentKey is not null && string.Equals(candidate, currentKey, StringComparison.OrdinalIgnoreCase))
        {
            return currentKey;
        }

        return await MakeUnique(kind, candidate, id);
    }

    public async Task<string> RepairIfUnusable(FileKeyKind kind, string name, Guid id, string? currentKey) =>
        FileKeyFactory.IsUsableFileKey(currentKey, Prefix(kind))
            ? currentKey!
            : await Allocate(kind, name, id);

    private async Task<string> MakeUnique(
        FileKeyKind kind,
        string candidate,
        Guid id,
        Func<string, bool>? alsoTaken = null)
    {
        var taken = await TakenKeysStartingWith(kind, candidate.ToLowerInvariant(), id);
        return FileKeyFactory.MakeUnique(candidate, key => taken.Contains(key) || (alsoTaken?.Invoke(key) ?? false));
    }

    private Task<HashSet<string>> TakenKeysStartingWith(FileKeyKind kind, string lowerPrefix, Guid id) =>
        kind switch
        {
            FileKeyKind.Podcast => Read(podcastRepository, lowerPrefix, id),
            FileKeyKind.TvShow => Read(tvShowRepository, lowerPrefix, id),
            FileKeyKind.NewsOrganisation => Read(newsOrganisationRepository, lowerPrefix, id),
            FileKeyKind.Film => Read(filmRepository, lowerPrefix, id),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
        };

    private static async Task<HashSet<string>> Read<TEntity>(
        IFilterableRepository<TEntity> repository,
        string lowerPrefix,
        Guid id) where TEntity : CosmosSelector
    {
        Expression<Func<TEntity, bool>> selector = x =>
            x.FileKey != null && x.FileKey.ToLower().StartsWith(lowerPrefix) && x.Id != id;
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await foreach (var key in repository.GetAllBy(selector, x => x.FileKey))
        {
            keys.Add(key);
        }

        return keys;
    }

    private static string Prefix(FileKeyKind kind) => kind switch
    {
        FileKeyKind.Podcast => string.Empty,
        FileKeyKind.TvShow => FileKeyFactory.TvShowPrefix,
        FileKeyKind.NewsOrganisation => FileKeyFactory.NewsOrganisationPrefix,
        FileKeyKind.Film => FileKeyFactory.FilmPrefix,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
    };
}

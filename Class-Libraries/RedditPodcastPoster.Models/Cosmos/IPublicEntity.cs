namespace RedditPodcastPoster.Models.Cosmos;

/// <summary>
/// A catalogue item published to the public database under its own file: a podcast, TV show, news organisation or
/// film (<see cref="Catalogue.Publisher"/>). Its <see cref="IFileKeyed.FileKey"/> is allocated by <c>IFileKeyAllocator</c>,
/// carries the kind's prefix (<see cref="FileKeyFactory.PrefixFor"/>) and is unique per kind. The setter stays on
/// <see cref="Catalogue.Publisher"/>; the allocator only reads it.
/// </summary>
public interface IPublicEntity : IFileKeyed
{
    Guid Id { get; }

    string Name { get; }
}

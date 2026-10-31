namespace RedditPodcastPoster.Models.Cosmos;

/// <summary>
/// A catalogue item published to the public database under its own file: a podcast, TV show, news organisation or
/// film (<see cref="Catalogue.Publisher"/>). Its <see cref="IFileKeyed.FileKey"/> is allocated by <c>IFileKeyAllocator</c>,
/// carries the kind's prefix (<see cref="FileKeyFactory.PrefixFor"/>) and is unique per kind.
/// </summary>
public interface IPublicEntity : IFileKeyed
{
    Guid Id { get; }

    string Name { get; }

    /// <inheritdoc cref="IFileKeyed.FileKey"/>
    new string FileKey { get; set; }
}

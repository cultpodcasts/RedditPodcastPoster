namespace RedditPodcastPoster.Models.Cosmos;

/// <summary>
/// A Cosmos document whose backup file (CosmosDbDownloader / <c>FileRepository</c>) or public-database file is named by
/// <see cref="FileKey"/>. Only these types store a <c>fileKey</c>: playables (episodes, news reports, TV episodes),
/// people, activity and discovery submissions are named by id and carry no file key.
/// </summary>
public interface IFileKeyed
{
    /// <summary>File name (without extension) of this document's backup or public file. JSON <c>fileKey</c>.</summary>
    string FileKey { get; }
}

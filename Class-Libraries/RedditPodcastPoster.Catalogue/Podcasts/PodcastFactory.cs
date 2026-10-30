using Podcast = RedditPodcastPoster.Models.Podcasts.Podcast;
using RedditPodcastPoster.Models.Cosmos;
using RedditPodcastPoster.Persistence.Abstractions.FileKeys;

namespace RedditPodcastPoster.Catalogue.Podcasts;

/// <summary>
/// Creates a podcast whose file key comes from <see cref="IFileKeyAllocator"/>: never empty (a name with no usable
/// Latin slug gets a key built from the new id, <see cref="FileKeyFactory.GetIdSlug"/>) and unique, ignoring case,
/// among stored podcasts and the keys this process has already handed out for podcasts it may not have saved yet.
/// </summary>
public class PodcastFactory(
    IFileKeyAllocator fileKeyAllocator) : IPodcastFactory
{
    private static readonly HashSet<string> IssuedFileKeys = new(StringComparer.OrdinalIgnoreCase);

    public async Task<Podcast> Create(string podcastName)
    {
        if (string.IsNullOrWhiteSpace(podcastName))
        {
            throw new ArgumentNullException(nameof(podcastName));
        }

        podcastName = podcastName.Trim();
        var id = Guid.NewGuid();
        while (true)
        {
            var fileKey = await fileKeyAllocator.Allocate(FileKeyKind.Podcast, podcastName, id, alsoTaken: IsIssued);
            // Claim atomically: a concurrent create may have issued the same key while this one awaited the store.
            if (TryClaim(fileKey))
            {
                return new Podcast { Id = id, Name = podcastName, FileKey = fileKey };
            }
        }
    }

    private static bool TryClaim(string fileKey)
    {
        lock (IssuedFileKeys)
        {
            return IssuedFileKeys.Add(fileKey);
        }
    }

    private static bool IsIssued(string fileKey)
    {
        lock (IssuedFileKeys)
        {
            return IssuedFileKeys.Contains(fileKey);
        }
    }
}

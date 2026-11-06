using Podcast = RedditPodcastPoster.Models.Podcasts.Podcast;
using RedditPodcastPoster.Models.Cosmos;
using RedditPodcastPoster.Persistence.Abstractions.FileKeys;

namespace RedditPodcastPoster.Catalogue.Podcasts;

/// <summary>
/// Creates a podcast whose file key comes from <see cref="IFileKeyAllocator"/>: never empty (a name with no usable
/// Latin slug gets a key built from the new id, <see cref="FileKeyFactory.GetIdSlug"/>) and unique, ignoring case,
/// among stored podcasts. The key is claimed until the podcast is saved. A create that is never saved releases
/// that claim when the allocator is disposed, so the next create of the same name can take the key (issue #1165).
/// </summary>
public class PodcastFactory(
    IFileKeyAllocator fileKeyAllocator) : IPodcastFactory
{
    public async Task<Podcast> Create(string podcastName)
    {
        if (string.IsNullOrWhiteSpace(podcastName))
        {
            throw new ArgumentNullException(nameof(podcastName));
        }

        podcastName = podcastName.Trim();
        var id = Guid.NewGuid();
        var fileKey = await fileKeyAllocator.Allocate(FileKeyKind.Podcast, podcastName, id);
        return new Podcast { Id = id, Name = podcastName, FileKey = fileKey };
    }
}

using Podcast = RedditPodcastPoster.Models.Podcasts.Podcast;
using RedditPodcastPoster.Models.Cosmos;
using RedditPodcastPoster.Persistence.Abstractions.FileKeys;

namespace RedditPodcastPoster.Catalogue.Podcasts;

/// <summary>
/// Creates a podcast whose file key comes from <see cref="IFileKeyAllocator"/>: never empty (a name with no usable
/// Latin slug gets a key built from the new id, <see cref="FileKeyFactory.GetIdSlug"/>) and unique, ignoring case,
/// among stored podcasts. Nothing is reserved until the podcast is saved: two creates of the same name that are not
/// saved in between (in this process or another) get the same key, and only the public-database publisher's
/// <c>PublicFileKeyGuard</c> catches that (cross-process claim: issue #1165).
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

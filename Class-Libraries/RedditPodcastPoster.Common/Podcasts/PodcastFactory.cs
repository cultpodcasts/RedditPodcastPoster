using RedditPodcastPoster.Models;
using RedditPodcastPoster.Models.Cosmos;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using Podcast = RedditPodcastPoster.Models.Podcast;

namespace RedditPodcastPoster.Common.Podcasts;

public class PodcastFactory(
    IPodcastRepository podcastRepository) : IPodcastFactory
{
    private static string[]? _fileKeys;

    public async Task<Podcast> Create(string podcastName)
    {
        if (string.IsNullOrWhiteSpace(podcastName))
        {
            throw new ArgumentNullException(nameof(podcastName));
        }

        _fileKeys ??= await podcastRepository.GetAll().Select(x => x.FileKey).ToArrayAsync();

        podcastName = podcastName.Trim();
        var fileKey = FileKeyFactory.GetFileKey(podcastName);
        if (_fileKeys.Contains(fileKey))
        {
            var rootFileKey = fileKey;
            var ctr = 2;
            do
            {
                fileKey = $"{rootFileKey}_{ctr++}";
            } while (_fileKeys.Contains(fileKey));
        }

        return new Podcast { Id = Guid.NewGuid(), Name = podcastName, FileKey = fileKey };
    }
}
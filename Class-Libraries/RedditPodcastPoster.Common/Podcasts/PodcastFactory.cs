using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence.Abstractions;

namespace RedditPodcastPoster.Common.Podcasts;

public class PodcastFactory(
    IPodcastRepository podcastRepository,
    ILogger<PodcastFactory> logger) : IPodcastFactory
{
    private static string[]? _fileKeys;

    public async Task<Podcast> Create(string podcastName)
    {
        if (string.IsNullOrWhiteSpace(podcastName))
        {
            throw new ArgumentNullException(nameof(podcastName));
        }

        _fileKeys ??= await podcastRepository.GetAllFileKeys().ToArrayAsync();

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

        return new Podcast(Guid.NewGuid()) {Name = podcastName, FileKey = fileKey};
    }
}
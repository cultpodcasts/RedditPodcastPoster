using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Common.Persistence;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.ModelTransformer.Models;

namespace RedditPodcastPoster.ModelTransformer;

public class ModelTransformer
{
    private static readonly Regex AlphaNumerics = new("[^a-zA-Z0-9 ]", RegexOptions.Compiled);
    private readonly FileRepository _fileRepository;
    private readonly ILogger<ModelTransformer> _logger;

    public ModelTransformer(FileRepository fileRepository, ILogger<ModelTransformer> logger)
    {
        _fileRepository = fileRepository;
        _logger = logger;
    }

    public async Task Run()
    {
        var podcasts = await _fileRepository.GetAll<OldPodcast>("old").ToListAsync();
        foreach (var oldPodcast in podcasts)
        {
            var newPodcast = new Podcast
            {
                Id= oldPodcast.Id,
                ModelType = ModelType.Podcast,
                AppleId = oldPodcast.AppleId,
                Bundles = oldPodcast.Bundles,
                DescriptionRegex = oldPodcast.DescriptionRegex,
                Name = oldPodcast.Name,
                SpotifyId = oldPodcast.SpotifyId,
                TitleRegex = oldPodcast.TitleRegex,
                YouTubeChannelId = oldPodcast.YouTubeChannelId,
                YouTubePublishingDelayTimeSpan = oldPodcast.YouTubePublishingDelayTimeSpan,
                FileKey= oldPodcast.FileKey,
            };
            newPodcast.Episodes = oldPodcast.Episodes.Select(oldEpisode => new Episode
                {
                    Id = oldEpisode.Id,
                    ModelType = oldEpisode.ModelType,
                    AppleId = oldEpisode.AppleId,
                    Description = oldEpisode.Description,
                    Explicit = oldEpisode.Explicit,
                    Length = oldEpisode.Length,
                    Posted = oldEpisode.Posted,
                    Ignored = oldEpisode.Ignored,
                    Release = oldEpisode.Release,
                    SpotifyId = oldEpisode.SpotifyId,
                    Title = oldEpisode.Title,
                    YouTubeId = oldEpisode.YouTubeId,
                    Urls = new ServiceUrls
                    {
                        Apple = oldEpisode.Urls.Apple,
                        Spotify = oldEpisode.Urls.Spotify,
                        YouTube = oldEpisode.Urls.YouTube
                    }
                }
            ).ToList();
            await _fileRepository.Write(
                "new",
                oldPodcast.FileKey,
                newPodcast);
        }
    }

    private string CleanseFileName(string newPodcastName)
    {
        var alphanumerics = AlphaNumerics.Replace(newPodcastName, "");
        var removedSpacing = alphanumerics.Replace("  ", "");
        return removedSpacing.Replace(" ", "_").ToLower();
    }
}
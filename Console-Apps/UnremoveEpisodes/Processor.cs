using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.EntitySearchIndexer;
using RedditPodcastPoster.Persistence.Abstractions;

namespace UnremoveEpisodes;

public partial class Processor(
    IEpisodeRepository episodeRepository,
    IEpisodeSearchIndexerService episodeSearchIndexerService,
    ILogger<Processor> logger)
{
    private readonly Regex episodeRecord = GenerateEpisodeRecordRegex();

    public async Task Process(Request request)
    {
        var episodes = await File.ReadAllLinesAsync(request.Filename);
        var updatedEpisodeIds = new List<Guid>();
        foreach (var episodeLine in episodes)
        {
            var match = episodeRecord.Match(episodeLine);
            if (match.Success)
            {
                var podcastName = match.Groups["podcast"].Value;
                var episodeTitle = match.Groups["episode"].Value;

                var episodeMatches = await episodeRepository
                    .GetAllBy(x =>
                        x.PodcastName != null &&
                        x.PodcastName.StartsWith(podcastName) &&
                        x.Title.StartsWith(episodeTitle) &&
                        x.Removed)
                    .ToListAsync();

                if (episodeMatches.Count != 1)
                {
                    logger.LogError("No singular ({count}) episode with title: '{episodeLine}'",
                        episodeMatches.Count, episodeLine);
                }
                else
                {
                    var episode = episodeMatches.Single();
                    episode.Removed = false;
                    updatedEpisodeIds.Add(episode.Id);
                    await episodeRepository.Save(episode);
                }
            }
            else
            {
                logger.LogError("Failed to match '{episodeLine}'", episodeLine);
            }
        }

        if (updatedEpisodeIds.Any())
        {
            await episodeSearchIndexerService.IndexEpisodes(updatedEpisodeIds, CancellationToken.None);
        }
    }

    [GeneratedRegex(@"^'(?<podcast>[\w\s&\D.']+)' - '(?<episode>[\w\s'|:!&#;\-,\(\.\/""\:\?'`\(\)]+)'\.$")]
    private static partial Regex GenerateEpisodeRecordRegex();
}
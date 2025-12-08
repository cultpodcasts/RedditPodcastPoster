using System.Text.RegularExpressions;
using Azure.Search.Documents;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.EntitySearchIndexer;
using RedditPodcastPoster.Persistence.Abstractions;

namespace UnremoveEpisodes;

public partial class Processor(
    SearchClient searchClient,
    IPodcastRepository podcastRepository,
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
                var podcast = await podcastRepository.GetBy(x =>
                    x.Name.StartsWith(podcastName) && x.Episodes.Any(y => y.Title.StartsWith(episodeTitle)));
                if (podcast != null)
                {
                    var episodeMatches = podcast.Episodes.Where(x => x.Title.StartsWith(episodeTitle) && x.Removed);
                    if (episodeMatches.Count() != 1)
                    {
                        logger.LogError("No singular ({count}) episode with title: '{episodeLine}'",
                            episodeMatches.Count(), episodeLine);
                    }
                    else
                    {
                        var episode= episodeMatches.Single();
                        episode.Removed = false;
                        updatedEpisodeIds.Add(episode.Id);
                        await podcastRepository.Save(podcast);
                    }

                }
                else
                {
                    logger.LogError("Failed to find podcast for episode-line: '{episodeLine}'", episodeLine);
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
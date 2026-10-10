using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;

namespace RemoveEpisodes.PodcastRestore;

/// <summary>
///     Resolves <c>--podcast-id</c> / <c>--podcast-name</c> to podcasts. Every target must resolve to exactly one
///     podcast; otherwise the result is <see langword="null" /> and the CLI exits 2 without writing.
/// </summary>
public class PodcastTargetResolver(
    IPodcastRepository podcastRepository,
    ILogger<PodcastTargetResolver> logger)
{
    public async Task<IReadOnlyList<Podcast>?> Resolve(
        IReadOnlyCollection<Guid> ids,
        IReadOnlyCollection<string> names,
        CancellationToken c)
    {
        if (ids.Count == 0 && names.Count == 0)
        {
            logger.LogError("Supply at least one --podcast-id or --podcast-name.");
            return null;
        }

        var resolved = new Dictionary<Guid, Podcast>();
        foreach (var id in ids)
        {
            c.ThrowIfCancellationRequested();
            var podcast = await podcastRepository.GetPodcast(id);
            if (podcast == null)
            {
                logger.LogError("No podcast with id '{podcastId}'.", id);
                return null;
            }

            resolved[podcast.Id] = podcast;
        }

        foreach (var name in names)
        {
            var trimmed = name.Trim();
            var matches = await podcastRepository.GetAllBy(x => x.Name == trimmed).ToListAsync(c);
            if (matches.Count != 1)
            {
                logger.LogError(
                    "Expected exactly one podcast named '{name}', found {count}{ids}. Use --podcast-id instead.",
                    trimmed, matches.Count,
                    matches.Count > 0 ? ": " + string.Join(", ", matches.Select(x => $"'{x.Id}'")) : string.Empty);
                return null;
            }

            resolved[matches[0].Id] = matches[0];
        }

        return resolved.Values.ToList();
    }
}

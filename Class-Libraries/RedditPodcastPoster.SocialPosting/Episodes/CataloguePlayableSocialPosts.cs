using Microsoft.Azure.Cosmos.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.Configuration.Extensions;
using RedditPodcastPoster.Configuration.Options;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.ContentKinds;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.PodcastServices.Abstractions.Extensions;

namespace RedditPodcastPoster.SocialPosting.Episodes;

public class CataloguePlayableSocialPosts(
    ITvShowRepository tvShowRepository,
    ITvShowEpisodeRepository tvShowEpisodeRepository,
    INewsOrganisationRepository newsOrganisationRepository,
    INewsReportRepository newsReportRepository,
    IFilmRepository filmRepository,
    IOptions<PostingCriteria> postingCriteria,
    ILogger<CataloguePlayableSocialPosts> logger)
    : ICataloguePlayableSocialPosts
{
    private readonly PostingCriteria _postingCriteria = postingCriteria.Value;

    public async Task<IReadOnlyList<PodcastEpisode>> GetUntweeted()
    {
        var items = await GetRecent(DateTimeExtensions.DaysAgo(_postingCriteria.TweetDays));
        return items.Where(x => !x.Episode.Tweeted).ToArray();
    }

    public async Task<IReadOnlyList<PodcastEpisode>> GetBlueskyReady()
    {
        var items = await GetRecent(DateTimeExtensions.DaysAgo(_postingCriteria.BlueSkyDays));
        return items.Where(x => !x.Episode.BlueskyPosted).ToArray();
    }

    public Task MarkTweeted(PodcastEpisode item) =>
        Update(item, playable => playable.Tweeted = true, film => film.Tweeted = true);

    public Task MarkBlueskyPosted(PodcastEpisode item, string blueskyPostUri) =>
        Update(item, playable => playable.BlueskyPost = blueskyPostUri, film => film.BlueskyPost = blueskyPostUri);

    private async Task<IReadOnlyList<PodcastEpisode>> GetRecent(DateTime since)
    {
        var items = new List<PodcastEpisode>();

        var tvShows = await tvShowRepository
            .GetAllBy(x => (!x.Removed.IsDefined() || x.Removed == null || x.Removed == false) &&
                           x.LatestReleased.IsDefined() && x.LatestReleased != null &&
                           x.LatestReleased >= since)
            .ToArrayAsync();
        foreach (var tvShow in tvShows)
        {
            var episodes = await tvShowEpisodeRepository
                .GetByTvShowId(tvShow.Id, x => x.ReleaseSort >= since)
                .ToArrayAsync();
            items.AddRange(episodes.Select(e => CatalogueParentKindMapper.ToPostProjection(e, tvShow)));
        }

        var organisations = await newsOrganisationRepository
            .GetAllBy(x => (!x.Removed.IsDefined() || x.Removed == null || x.Removed == false) &&
                           x.LatestReleased.IsDefined() && x.LatestReleased != null &&
                           x.LatestReleased >= since)
            .ToArrayAsync();
        foreach (var organisation in organisations)
        {
            var reports = await newsReportRepository
                .GetByNewsOrganisationId(organisation.Id, x => x.ReleaseSort >= since)
                .ToArrayAsync();
            items.AddRange(reports.Select(r => CatalogueParentKindMapper.ToPostProjection(r, organisation)));
        }

        // A film has no releaseCosmosFallback: a document written before releaseSort existed only has
        // release, and its ReleaseSort is rebuilt from that on read. Fetch those too and let the
        // in-memory ReleaseUtc check below apply the window.
        var films = await filmRepository
            .GetAllBy(x => (!x.Removed.IsDefined() || x.Removed == null || x.Removed == false) &&
                           (!x.ReleaseSort.IsDefined() || x.ReleaseSort >= since))
            .ToArrayAsync();
        items.AddRange(films.Select(CatalogueParentKindMapper.ToPostProjection));

        // Same readiness as the podcast filter (PodcastEpisodeFilter): in the window, not removed or ignored,
        // a postable link, and not held back by a YouTube publishing delay.
        var postable = items
            .Where(x => x.Episode.ReleaseUtc >= since &&
                        x.Episode is { Removed: false, Ignored: false } &&
                        EpisodeServicePresence.TryGetPreferredSocialPostUrl(x.Episode, out _, out _) &&
                        !x.Podcast.IsDelayedYouTubePublishing(x.Episode))
            .OrderByDescending(x => x.Episode.ReleaseUtc)
            .ToArray();
        logger.LogInformation(
            "Catalogue social-post candidates since '{Since:O}': tv-shows {TvShows}, news-organisations {Organisations}, films {Films}, items {Items}, postable {Postable}, skipped {Skipped}.",
            since, tvShows.Length, organisations.Length, films.Length, items.Count, postable.Length,
            items.Count - postable.Length);
        return postable;
    }

    private async Task Update(PodcastEpisode item, Action<Playable> setPlayable, Action<Models.Films.Film> setFilm)
    {
        ArgumentNullException.ThrowIfNull(item);
        switch (item.ContentKind)
        {
            case nameof(ContentKind.TvShowEpisode):
            {
                var stored = await tvShowEpisodeRepository.GetTvShowEpisode(item.Podcast.Id, item.Episode.Id)
                             ?? throw NotFound(item);
                setPlayable(stored);
                await tvShowEpisodeRepository.Save(stored);
                break;
            }
            case nameof(ContentKind.NewsReport):
            {
                var stored = await newsReportRepository.GetNewsReport(item.Podcast.Id, item.Episode.Id)
                             ?? throw NotFound(item);
                setPlayable(stored);
                await newsReportRepository.Save(stored);
                break;
            }
            case nameof(ContentKind.Film):
            {
                // Re-read on 412. The flag is applied to the film that is saved, so a trailer attached since the
                // post was composed is still on that film. The episode row for that trailer is already deleted.
                if (await filmRepository.Update(item.Episode.Id, stored =>
                    {
                        setFilm(stored);
                        return true;
                    }) is null)
                {
                    throw NotFound(item);
                }

                break;
            }
            default:
                throw new ArgumentException(
                    $"'{item.ContentKind}' is not a catalogue playable kind; podcast episodes are saved through the episode repository.",
                    nameof(item));
        }
    }

    private static InvalidOperationException NotFound(PodcastEpisode item) =>
        new($"{item.ContentKind} '{item.Episode.Id}' (parent '{item.Podcast.Id}') was not found when recording its social post.");
}

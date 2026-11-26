using Microsoft.Azure.Cosmos.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.Configuration.Extensions;
using RedditPodcastPoster.Configuration.Options;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.ContentKinds;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.News;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Models.TvShows;
using RedditPodcastPoster.Persistence.Abstractions.Episodes;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;

namespace RedditPodcastPoster.SocialPosting.Episodes;

public class CataloguePlayableSocialPosts(
    ITvShowRepository tvShowRepository,
    ITvShowEpisodeRepository tvShowEpisodeRepository,
    INewsOrganisationRepository newsOrganisationRepository,
    INewsReportRepository newsReportRepository,
    IFilmRepository filmRepository,
    IPodcastEpisodeFilter podcastEpisodeFilter,
    IOptions<PostingCriteria> postingCriteria,
    ILogger<CataloguePlayableSocialPosts> logger)
    : ICataloguePlayableSocialPosts
{
    private readonly PostingCriteria _postingCriteria = postingCriteria.Value;

    public async Task<IReadOnlyList<PodcastEpisode>> GetUntweeted()
    {
        var candidates = await GetCandidates(DateTimeExtensions.DaysAgo(_postingCriteria.TweetDays));
        return await FilterReady(
            candidates,
            _postingCriteria.TweetDays,
            podcastEpisodeFilter.GetMostRecentUntweetedEpisodes);
    }

    public async Task<IReadOnlyList<PodcastEpisode>> GetBlueskyReady()
    {
        var candidates = await GetCandidates(DateTimeExtensions.DaysAgo(_postingCriteria.BlueSkyDays));
        return await FilterReady(
            candidates,
            _postingCriteria.BlueSkyDays,
            podcastEpisodeFilter.GetMostRecentBlueskyReadyEpisodes);
    }

    public Task MarkTweeted(PodcastEpisode item, string? tweetPost) =>
        Update(item, playable => SetTweeted(playable, tweetPost), film => SetTweeted(film, tweetPost));

    public Task MarkNotTweetable(PodcastEpisode item, string reason) =>
        Update(item, playable => SetNotTweetable(playable, reason), film => SetNotTweetable(film, reason));

    private static void SetTweeted(IPromotable stored, string? tweetPost)
    {
        if (!string.IsNullOrWhiteSpace(tweetPost))
        {
            stored.TweetPost = tweetPost;
            return;
        }

        stored.OldTweeted = true;
    }

    private static void SetNotTweetable(IPromotable stored, string reason) =>
        PromotableTweetSkip.ApplyNotTweetable(stored, reason);

    public Task MarkBlueskyPosted(PodcastEpisode item, string blueskyPostUri) =>
        Update(item, playable => playable.BlueskyPost = blueskyPostUri, film => film.BlueskyPost = blueskyPostUri);

    public Task ClearIgnoredAndRemoved(PodcastEpisode item) =>
        Update(
            item,
            playable =>
            {
                playable.Ignored = false;
                playable.Removed = false;
            },
            film =>
            {
                film.Ignored = false;
                film.Removed = false;
            });

    private async Task<IReadOnlyList<PodcastEpisode>> GetCandidates(DateTime since)
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
                .GetByTvShowId(tvShow.Id, PlayableCosmosFilters.ReleasedOnOrAfter<TvShowEpisode>(since))
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
                .GetByNewsOrganisationId(
                    organisation.Id,
                    PlayableCosmosFilters.ReleasedOnOrAfter<NewsReport>(since))
                .ToArrayAsync();
            items.AddRange(reports.Select(r => CatalogueParentKindMapper.ToPostProjection(r, organisation)));
        }

        // A film has no releaseCosmosFallback: a document written before releaseSort existed only has
        // release, and its ReleaseSort is rebuilt from that on read. Fetch those too and let the
        // podcast filter's ReleaseUtc window apply.
        var films = await filmRepository
            .GetAllBy(x => (!x.Removed.IsDefined() || x.Removed == null || x.Removed == false) &&
                           (!x.ReleaseSort.IsDefined() || x.ReleaseSort >= since))
            .ToArrayAsync();
        items.AddRange(films.Select(CatalogueParentKindMapper.ToPostProjection));

        logger.LogInformation(
            "Catalogue social-post candidates since '{Since:O}': tv-shows {TvShows}, news-organisations {Organisations}, films {Films}, items {Items}.",
            since, tvShows.Length, organisations.Length, films.Length, items.Count);
        return items;
    }

    /// <summary>
    /// Applies the same podcast readiness rules via <see cref="IPodcastEpisodeFilter"/>, then restores
    /// <see cref="PodcastEpisode.ContentKind"/> (the filter rewraps pairs without it).
    /// </summary>
    private async Task<IReadOnlyList<PodcastEpisode>> FilterReady(
        IReadOnlyList<PodcastEpisode> candidates,
        int numberOfDays,
        Func<Podcast, IEnumerable<Episode>, int, Task<IEnumerable<PodcastEpisode>>> filter)
    {
        if (candidates.Count == 0)
        {
            return [];
        }

        var contentKindByEpisodeId = candidates.ToDictionary(x => x.Episode.Id, x => x.ContentKind);
        var ready = new List<PodcastEpisode>();
        foreach (var group in candidates.GroupBy(x => x.Podcast.Id))
        {
            var podcast = group.First().Podcast;
            var filtered = await filter(podcast, group.Select(x => x.Episode), numberOfDays);
            foreach (var item in filtered)
            {
                if (!contentKindByEpisodeId.TryGetValue(item.Episode.Id, out var contentKind))
                {
                    throw new InvalidOperationException(
                        $"Catalogue filter returned episode '{item.Episode.Id}' with no ContentKind among the candidates; Update requires ContentKind.");
                }

                ready.Add(item with { ContentKind = contentKind });
            }
        }

        var ordered = ready.OrderByDescending(x => x.Episode.ReleaseUtc).ToArray();
        logger.LogInformation(
            "Catalogue social-post filter: candidates {Candidates}, ready {Ready}, skipped {Skipped}.",
            candidates.Count, ordered.Length, candidates.Count - ordered.Length);
        return ordered;
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

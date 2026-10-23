using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.Configuration.Extensions;
using RedditPodcastPoster.Configuration.Options;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.News;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Models.TvShows;
using RedditPodcastPoster.Persistence.Abstractions.Episodes;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;

namespace RedditPodcastPoster.Subjects.Categorisation;

/// <summary>
/// Recent TV episodes and news reports with no subjects, in the categoriser-days window.
/// The parent indexer stores new playables with empty subjects and does not put them on the
/// podcast <c>RecentEpisodeCandidates</c> list. Posting reads the containers itself.
/// </summary>
public class RecentCataloguePlayableCategoriser(
    ICategoriser categoriser,
    ITvShowRepository tvShowRepository,
    ITvShowEpisodeRepository tvShowEpisodeRepository,
    INewsOrganisationRepository newsOrganisationRepository,
    INewsReportRepository newsReportRepository,
    IOptions<PostingCriteria> postingCriteria,
    ILogger<RecentCataloguePlayableCategoriser> logger)
    : IRecentCataloguePlayableCategoriser
{
    public async Task<IList<Guid>> Categorise()
    {
        var since = DateTimeExtensions.DaysAgo(postingCriteria.Value.CategoriserDays);
        var updated = new List<Guid>();

        var shows = await Collect(tvShowRepository.GetAllBy(PublisherCosmosFilters.ReleasedOnOrAfter<TvShow>(since)));
        foreach (var show in shows.Where(show => ParentInWindow(show, since)))
        {
            var episodes = await Collect(tvShowEpisodeRepository.GetByTvShowId(
                show.Id, PlayableCosmosFilters.ReleasedOnOrAfter<TvShowEpisode>(since)));
            updated.AddRange(await CategoriseParent(show, episodes, since, tvShowEpisodeRepository.Save));
        }

        var organisations = await Collect(newsOrganisationRepository.GetAllBy(
            PublisherCosmosFilters.ReleasedOnOrAfter<NewsOrganisation>(since)));
        foreach (var organisation in organisations.Where(organisation => ParentInWindow(organisation, since)))
        {
            var reports = await Collect(newsReportRepository.GetByNewsOrganisationId(
                organisation.Id, PlayableCosmosFilters.ReleasedOnOrAfter<NewsReport>(since)));
            updated.AddRange(await CategoriseParent(organisation, reports, since, newsReportRepository.Save));
        }

        return updated;
    }

    private async Task<IReadOnlyList<Guid>> CategoriseParent<TPlayable>(
        Publisher parent,
        IReadOnlyList<TPlayable> playables,
        DateTime since,
        Func<TPlayable, Task> save)
        where TPlayable : Playable
    {
        var eligible = playables.Where(playable => NeedsCategorising(playable, since)).ToList();
        if (eligible.Count == 0)
        {
            return [];
        }

        var updated = new List<Guid>();
        var deltas = new List<CategoriseEpisodeDelta>();
        foreach (var playable in eligible)
        {
            var projection = Project(playable, parent);
            var podcast = PodcastForCategorisation(parent, projection.Podcast);
            var before = playable.Subjects.ToArray();
            var changed = await categoriser.Categorise(projection.Episode, podcast);
            if (changed)
            {
                playable.Subjects = [.. projection.Episode.Subjects];
                playable.Matches = projection.Episode.Matches.Select(match => match.Clone()).ToList();
                await save(playable);
                updated.Add(playable.Id);
            }

            deltas.Add(CategoriseEpisodeDelta.From(
                playable.Id,
                playable.Title,
                before,
                playable.Subjects.ToArray(),
                changed));
        }

        CategorisePodcastLogger.LogCatalogue(logger, parent.Id, parent.Name, deltas);
        return updated;
    }

    /// <summary>
    /// Discovery settings (including the description regex) live on <c>indexing</c>. The post
    /// projection only copies publisher fields, so the default subject is on both.
    /// </summary>
    private static Podcast PodcastForCategorisation(Publisher parent, Podcast projected)
    {
        var indexing = parent switch
        {
            TvShow show => show.Indexing,
            NewsOrganisation organisation => organisation.Indexing,
            _ => null
        };
        return indexing?.ToTransientPodcast(parent) ?? projected;
    }

    private static PodcastEpisode Project(Playable playable, Publisher parent) => playable switch
    {
        TvShowEpisode episode when parent is TvShow show =>
            CatalogueParentKindMapper.ToPostProjection(episode, show),
        NewsReport report when parent is NewsOrganisation organisation =>
            CatalogueParentKindMapper.ToPostProjection(report, organisation),
        _ => throw new InvalidOperationException(
            $"Cannot categorise playable '{playable.Id}' ({playable.GetType().Name}) under parent '{parent.Id}' ({parent.GetType().Name}).")
    };

    private static bool ParentInWindow(Publisher parent, DateTime since) =>
        !parent.IsRemoved() &&
        parent.LatestReleased is { } latest &&
        latest >= since;

    /// <summary>
    /// Same eligibility as recent podcast episodes: in the window, not ignored or removed, and no subjects yet.
    /// </summary>
    private static bool NeedsCategorising(Playable playable, DateTime since) =>
        !playable.Ignored &&
        !playable.IsRemoved() &&
        playable.ReleaseSort >= since &&
        playable.Subjects.Count == 0;

    private static async Task<List<T>> Collect<T>(IAsyncEnumerable<T> source)
    {
        var items = new List<T>();
        await foreach (var item in source)
        {
            items.Add(item);
        }

        return items;
    }
}

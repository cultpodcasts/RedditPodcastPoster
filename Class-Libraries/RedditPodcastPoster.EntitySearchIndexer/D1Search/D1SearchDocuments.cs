using RedditPodcastPoster.EntitySearchIndexer.Extensions;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Films;
using RedditPodcastPoster.Models.News;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Models.TvShows;
using RedditPodcastPoster.Search.Models;

namespace RedditPodcastPoster.EntitySearchIndexer.D1Search;

/// <summary>
/// D1 search documents, built from the <b>same</b> push-path documents Azure gets
/// (<see cref="PodcastEpisodeExtensions.ToEpisodeSearchRecord"/>, <see cref="CataloguePlayableSearchDocument"/>), so the
/// field rules (images, service ids, <c>svc</c>, language, film trailers and AKAs, identity ids) live in one place.
/// Only three things differ, all decided for D1 (SP-0.3, 16 Nov 2026): the description is the full text, not the
/// Azure cut; guests are indexed; and <c>modifiedAt</c> is the playable's Cosmos <c>_ts</c> in milliseconds.
/// Whether a playable belongs in the index at all is <see cref="D1SearchEligibility"/>, not decided here.
/// </summary>
public static class D1SearchDocuments
{
    public static D1SearchDocument For(Podcast podcast, Episode episode)
    {
        ArgumentNullException.ThrowIfNull(podcast);
        ArgumentNullException.ThrowIfNull(episode);
        var record = new PodcastEpisode(podcast, episode).ToEpisodeSearchRecord(includeUnifiedPlayableFields: true);
        return From(record, episode.Description, episode.Guests, episode.Timestamp);
    }

    public static D1SearchDocument For(TvShowEpisode episode) =>
        From(CataloguePlayableSearchDocument.For(episode), episode.Description, episode.Guests, episode.Timestamp);

    public static D1SearchDocument For(NewsReport report) =>
        From(CataloguePlayableSearchDocument.For(report), report.Description, report.Guests, report.Timestamp);

    public static D1SearchDocument For(Film film) =>
        From(CataloguePlayableSearchDocument.For(film), film.Description, film.Guests, film.Timestamp);

    /// <summary>
    /// The Azure document with the full description, the guests and <c>modifiedAt</c>. A blank description is
    /// sent as an empty string (as Azure has it); blank guests are dropped and an empty list is sent as null.
    /// </summary>
    public static D1SearchDocument From(
        EpisodeSearchRecord record,
        string? fullDescription,
        IEnumerable<string>? guests,
        long cosmosTimestampSeconds)
    {
        ArgumentNullException.ThrowIfNull(record);
        var guestList = guests?.Where(g => !string.IsNullOrWhiteSpace(g)).Select(g => g.Trim()).ToArray();
        return new D1SearchDocument
        {
            Id = record.Id,
            ContentKind = record.ContentKind,
            Title = record.Title ?? record.EpisodeTitle,
            SeriesName = record.SeriesName ?? record.PodcastName,
            Description = fullDescription?.Trim() ?? string.Empty,
            Release = record.Release,
            Duration = record.Duration,
            SpotifyId = record.SpotifyId,
            AppleId = record.AppleId,
            PodcastAppleId = record.PodcastAppleId,
            YoutubeId = record.YoutubeId,
            Bbc = record.BBC,
            InternetArchive = record.InternetArchive,
            Svc = record.Svc,
            Subjects = record.Subjects,
            Image = record.Image,
            Trailers = record.Trailers,
            AlternativeTitles = record.AlternativeTitles,
            ImdbId = record.ImdbId,
            TmdbId = record.TmdbId,
            TvdbId = record.TvdbId,
            SeriesImdbId = record.SeriesImdbId,
            SeriesTmdbId = record.SeriesTmdbId,
            SeriesTvdbId = record.SeriesTvdbId,
            Lang = record.Lang,
            Guests = guestList is { Length: > 0 } ? guestList : null,
            PublisherSearchTerms = record.PublisherSearchTerms,
            EpisodeSearchTerms = record.EpisodeSearchTerms,
            ModifiedAt = Math.Max(0, cosmosTimestampSeconds) * 1000
        };
    }
}

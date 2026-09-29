using RedditPodcastPoster.Models.Cosmos;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Films;
using RedditPodcastPoster.Models.News;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Models.Services;
using RedditPodcastPoster.Models.TvShows;

namespace RedditPodcastPoster.Models.Catalogue;

/// <summary>
/// Maps a Podcast (+ episodes) onto a TvShow or NewsOrganisation parent,
/// or a one-off episode onto a Film (no parent). Parent id is the podcast id;
/// each playable keeps its episode id. Film is never a parent.
/// </summary>
public static class CatalogueParentKindMapper
{
    public static TvShow ToTvShow(Podcast podcast)
    {
        ArgumentNullException.ThrowIfNull(podcast);
        var show = new TvShow(podcast.Name)
        {
            Id = podcast.Id
        };
        CopyPublisher(podcast, show);
        show.FileKey = FileKeyFactory.GetTvShowFileKey(podcast.Name);
        show.ModelType = ModelType.TvShow;
        return show;
    }

    public static NewsOrganisation ToNewsOrganisation(Podcast podcast)
    {
        ArgumentNullException.ThrowIfNull(podcast);
        var organisation = new NewsOrganisation(podcast.Name)
        {
            Id = podcast.Id
        };
        CopyPublisher(podcast, organisation);
        organisation.FileKey = FileKeyFactory.GetNewsOrganisationFileKey(podcast.Name);
        organisation.ModelType = ModelType.NewsOrganisation;
        return organisation;
    }

    public static TvShowEpisode ToTvShowEpisode(Episode episode, TvShow tvShow)
    {
        ArgumentNullException.ThrowIfNull(episode);
        ArgumentNullException.ThrowIfNull(tvShow);
        var playable = new TvShowEpisode(episode.Title)
        {
            Id = episode.Id
        };
        CopyPlayable(episode, playable);
        playable.SetTvShowProperties(tvShow);
        playable.ModelType = ModelType.TvShowEpisode;
        return playable;
    }

    /// <summary>
    /// One-off film: keep the episode id (playable identity). Name and description
    /// come from the episode; publisher social fields come from the podcast.
    /// </summary>
    public static Film ToFilm(Podcast podcast, Episode episode)
    {
        ArgumentNullException.ThrowIfNull(podcast);
        ArgumentNullException.ThrowIfNull(episode);
        var film = new Film(episode.Title)
        {
            Id = episode.Id
        };
        CopyPublisher(podcast, film);
        film.Name = episode.Title;
        film.Description = episode.Description;
        film.Language = episode.Language;
        film.SearchTerms = episode.SearchTerms;
        film.HashTag = episode.HashTag ?? podcast.HashTag;
        film.FileKey = FileKeyFactory.GetFilmFileKey(episode.Title);
        film.ModelType = ModelType.Film;
        film.SetRelease(episode.Release);
        film.Length = episode.Length;
        film.Explicit = episode.Explicit;
        film.Posted = episode.Posted;
        film.Tweeted = episode.Tweeted;
        film.OldBlueskyPosted = episode.OldBlueskyPosted;
        film.BlueskyPost = episode.BlueskyPost;
        film.Ignored = episode.Ignored;
        film.Subjects = [.. episode.Subjects];
        film.RemovedSubjects = [.. episode.RemovedSubjects];
        film.Matches = episode.Matches.Select(CloneMatch).ToList();
        film.Services = CloneServices(episode.Services);
        film.Guests = CloneArray(episode.Guests);
        return film;
    }

    public static NewsReport ToNewsReport(Episode episode, NewsOrganisation organisation)
    {
        ArgumentNullException.ThrowIfNull(episode);
        ArgumentNullException.ThrowIfNull(organisation);
        var playable = new NewsReport(episode.Title)
        {
            Id = episode.Id
        };
        CopyPlayable(episode, playable);
        playable.SetNewsOrganisationProperties(organisation);
        playable.ModelType = ModelType.NewsReport;
        return playable;
    }

    private static void CopyPublisher(Publisher source, Publisher target)
    {
        target.Name = source.Name;
        target.Description = source.Description;
        target.LatestReleased = source.LatestReleased;
        target.Language = source.Language;
        target.LastIndexed = source.LastIndexed;
        target.Removed = source.Removed;
        target.PublisherName = source.PublisherName;
        target.TwitterHandle = source.TwitterHandle;
        target.BlueskyHandle = source.BlueskyHandle;
        target.HashTag = source.HashTag;
        target.EnrichmentHashTags = CloneArray(source.EnrichmentHashTags);
        target.IgnoredAssociatedSubjects = CloneArray(source.IgnoredAssociatedSubjects);
        target.IgnoredSubjects = CloneArray(source.IgnoredSubjects);
        target.DefaultSubject = source.DefaultSubject;
        target.SearchTerms = source.SearchTerms;
        target.KnownTerms = CloneArray(source.KnownTerms);
    }

    private static void CopyPlayable(Playable source, Playable target)
    {
        target.Title = source.Title;
        target.Description = source.Description;
        target.SetRelease(source.Release);
        target.Length = source.Length;
        target.Explicit = source.Explicit;
        target.Posted = source.Posted;
        target.Tweeted = source.Tweeted;
        target.OldBlueskyPosted = source.OldBlueskyPosted;
        target.BlueskyPost = source.BlueskyPost;
        target.Ignored = source.Ignored;
        target.Removed = source.Removed;
        target.Language = source.Language;
        target.Subjects = [.. source.Subjects];
        target.RemovedSubjects = [.. source.RemovedSubjects];
        target.Matches = source.Matches.Select(CloneMatch).ToList();
        target.SearchTerms = source.SearchTerms;
        target.HashTag = source.HashTag;
        target.PublisherSearchTerms = source.PublisherSearchTerms;
        target.PublisherLanguage = source.PublisherLanguage;
        target.ParentMetadataVersion = source.ParentMetadataVersion;
        target.ParentRemoved = source.ParentRemoved;
        target.Services = CloneServices(source.Services);
        target.Guests = CloneArray(source.Guests);
    }

    private static string[]? CloneArray(string[]? values) =>
        values is null ? null : [.. values];

    private static PlayableSubjectMatch CloneMatch(PlayableSubjectMatch match) =>
        new()
        {
            Subject = match.Subject,
            Term = match.Term,
            Source = match.Source
        };

    private static Dictionary<string, ServiceLink>? CloneServices(Dictionary<string, ServiceLink>? services)
    {
        if (services is null)
        {
            return null;
        }

        return services.ToDictionary(
            pair => pair.Key,
            pair => new ServiceLink
            {
                Url = pair.Value.Url,
                Image = pair.Value.Image,
                Language = pair.Value.Language
            });
    }
}

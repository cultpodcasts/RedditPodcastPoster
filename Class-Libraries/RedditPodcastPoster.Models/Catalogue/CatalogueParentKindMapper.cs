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
/// TV and News parents keep the podcast's discovery settings (<see cref="CatalogueIndexingSource"/>)
/// so the hourly indexer can keep finding new playables.
/// </summary>
public static class CatalogueParentKindMapper
{
    public static TvShow ToTvShow(Podcast podcast)
    {
        ArgumentNullException.ThrowIfNull(podcast);
        var showName = CatalogueTvShowCanonicalNames.ShowNameFor(podcast.Name);
        var show = new TvShow(showName)
        {
            Id = podcast.Id
        };
        CopyPublisher(podcast, show);
        show.Name = showName;
        show.FileKey = FileKeyFactory.GetTvShowFileKey(showName);
        show.ModelType = ModelType.TvShow;
        show.Indexing = CatalogueIndexingSource.FromPodcast(podcast);
        return show;
    }

    public static NewsOrganisation ToNewsOrganisation(Podcast podcast)
    {
        ArgumentNullException.ThrowIfNull(podcast);
        var organisationName = CatalogueNewsOrganisationChannelAliases.OrganisationNameFor(podcast.Name);
        var organisation = new NewsOrganisation(organisationName)
        {
            Id = podcast.Id
        };
        CopyPublisher(podcast, organisation);
        organisation.Name = organisationName;
        organisation.FileKey = FileKeyFactory.GetNewsOrganisationFileKey(organisationName);
        organisation.ModelType = ModelType.NewsOrganisation;
        organisation.Indexing = CatalogueIndexingSource.FromPodcast(podcast);
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
        playable.YouTubeId = EpisodeServicePresence.CatalogueYouTubeIdForCreate(episode);
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
        film.YouTubeId = EpisodeServicePresence.CatalogueYouTubeIdForCreate(episode);
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
        playable.YouTubeId = EpisodeServicePresence.CatalogueYouTubeIdForCreate(episode);
        playable.SetNewsOrganisationProperties(organisation);
        playable.ModelType = ModelType.NewsReport;
        return playable;
    }

    /// <summary>
    /// Read-only social-post projection of a TV episode: the show stands in for the podcast.
    /// Never save the projection; posted state is written back to the TV episode.
    /// </summary>
    public static PodcastEpisode ToPostProjection(TvShowEpisode playable, TvShow tvShow) =>
        ToPostProjection(playable, tvShow, nameof(ContentKinds.ContentKind.TvShowEpisode));

    /// <summary>Read-only social-post projection of a news report: the organisation stands in for the podcast.</summary>
    public static PodcastEpisode ToPostProjection(NewsReport playable, NewsOrganisation organisation) =>
        ToPostProjection(playable, organisation, nameof(ContentKinds.ContentKind.NewsReport));

    /// <summary>
    /// Read-only social-post projection of a film. A film has no parent, so the film's own
    /// publisher fields (handles, hashtags) stand in for the podcast and its name is the title.
    /// </summary>
    public static PodcastEpisode ToPostProjection(Film film)
    {
        ArgumentNullException.ThrowIfNull(film);
        var podcast = new Podcast { Id = film.Id };
        CopyPublisher(film, podcast);
        var episode = new Episode
        {
            Id = film.Id,
            Title = film.Name,
            Description = film.Description,
            Length = film.Length,
            Explicit = film.Explicit,
            Posted = film.Posted,
            Tweeted = film.Tweeted,
            OldBlueskyPosted = film.OldBlueskyPosted,
            BlueskyPost = film.BlueskyPost,
            Ignored = film.Ignored,
            Removed = film.IsRemoved(),
            Language = film.Language,
            Subjects = [.. film.Subjects],
            RemovedSubjects = [.. film.RemovedSubjects],
            Matches = film.Matches.Select(CloneMatch).ToList(),
            SearchTerms = film.SearchTerms,
            HashTag = film.HashTag,
            Services = CloneServices(film.Services),
            Guests = CloneArray(film.Guests)
        };
        episode.SetRelease(film.Release);
        episode.SetPodcastProperties(podcast);
        return new PodcastEpisode(podcast, episode) { ContentKind = nameof(ContentKinds.ContentKind.Film) };
    }

    private static PodcastEpisode ToPostProjection(Playable playable, Publisher parent, string contentKind)
    {
        ArgumentNullException.ThrowIfNull(playable);
        ArgumentNullException.ThrowIfNull(parent);
        var podcast = new Podcast { Id = parent.Id };
        CopyPublisher(parent, podcast);
        var episode = new Episode { Id = playable.Id };
        CopyPlayable(playable, episode);
        episode.SetPodcastProperties(podcast);
        return new PodcastEpisode(podcast, episode) { ContentKind = contentKind };
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
        CopyCatalogueYouTubeId(source, target);
    }

    private static void CopyCatalogueYouTubeId(Playable source, Playable target)
    {
        if (source is not ICatalogueYouTubeItem from || target is not ICatalogueYouTubeItem to)
        {
            return;
        }

        to.YouTubeId = string.IsNullOrWhiteSpace(from.YouTubeId) ? null : from.YouTubeId;
    }

    private static string[]? CloneArray(string[]? values) =>
        values is null ? null : [.. values];

    private static PlayableSubjectMatch CloneMatch(PlayableSubjectMatch match) => match.Clone();

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

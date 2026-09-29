using RedditPodcastPoster.Models.Cosmos;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.News;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Models.Services;
using RedditPodcastPoster.Models.TvShows;

namespace RedditPodcastPoster.Models.Catalogue;

/// <summary>
/// Maps a Podcast (+ episodes) onto a TvShow or NewsOrganisation parent.
/// Parent id is the podcast id; each playable keeps its episode id. Never Film.
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

namespace Api.Dtos.Extensions;

public static class RequestModelExtensions
{
    public static Models.PersonChangeRequest ToModel(this PersonChangeRequest request) => new()
    {
        Id = request.Id,
        Name = request.Name,
        SortName = request.SortName,
        IsOrganization = request.IsOrganization,
        Aliases = request.Aliases,
        TwitterHandle = request.TwitterHandle,
        BlueskyHandle = request.BlueskyHandle
    };

    public static Models.EpisodeChangeRequest ToModel(this EpisodeChangeRequest request) => new()
    {
        Title = request.Title,
        Description = request.Description,
        Posted = request.Posted,
        Tweeted = request.Tweeted,
        UnBluesky = request.UnBluesky,
        Ignored = request.Ignored,
        Removed = request.Removed,
        Explicit = request.Explicit,
        Release = request.Release,
        Duration = request.Duration,
        Urls = request.Urls,
        Images = request.Images,
        Services = request.Services,
        Subjects = request.Subjects,
        SearchTerms = request.SearchTerms,
        HashTag = request.HashTag,
        Language = request.Language,
        Guests = request.Guests
    };

    public static Models.EpisodePublishRequest ToModel(this EpisodePublishRequest request) => new()
    {
        Post = request.Post,
        Tweet = request.Tweet,
        BlueskyPost = request.BlueskyPost
    };

    public static Models.PodcastChangeRequest ToModel(this PodcastChangeRequest request) => new()
    {
        Id = request.Id,
        Name = request.Name,
        Language = request.Language,
        Removed = request.Removed,
        IndexAllEpisodes = request.IndexAllEpisodes,
        BypassShortEpisodeChecking = request.BypassShortEpisodeChecking,
        AlwaysPromoteAsHero = request.AlwaysPromoteAsHero,
        ReleaseAuthority = request.ReleaseAuthority,
        UnsetReleaseAuthority = request.UnsetReleaseAuthority,
        PrimaryPostService = request.PrimaryPostService,
        UnsetPrimaryPostService = request.UnsetPrimaryPostService,
        SpotifyId = request.SpotifyId,
        AppleId = request.AppleId,
        NullAppleId = request.NullAppleId,
        YouTubePublishingDelayTimeSpan = request.YouTubePublishingDelayTimeSpan,
        SkipEnrichingFromYouTube = request.SkipEnrichingFromYouTube,
        TwitterHandle = request.TwitterHandle,
        BlueskyHandle = request.BlueskyHandle,
        EnrichmentHashTags = request.EnrichmentHashTags,
        HashTag = request.HashTag,
        TitleRegex = request.TitleRegex,
        DescriptionRegex = request.DescriptionRegex,
        EpisodeMatchRegex = request.EpisodeMatchRegex,
        EpisodeIncludeTitleRegex = request.EpisodeIncludeTitleRegex,
        DefaultSubject = request.DefaultSubject,
        IgnoreAllEpisodes = request.IgnoreAllEpisodes,
        YouTubeChannelId = request.YouTubeChannelId,
        YouTubePlaylistId = request.YouTubePlaylistId,
        IgnoredAssociatedSubjects = request.IgnoredAssociatedSubjects,
        IgnoredSubjects = request.IgnoredSubjects,
        KnownTerms = request.KnownTerms,
        MinimumDuration = request.MinimumDuration
    };

    public static Models.PodcastKindTransferRequest ToModel(this PodcastKindTransferRequest request) => new()
    {
        TargetKind = request.TargetKind
    };

    public static Models.SubjectChangeRequest ToModel(this SubjectChangeRequest request) => new()
    {
        Id = request.Id,
        Aliases = request.Aliases,
        AssociatedSubjects = request.AssociatedSubjects,
        Name = request.Name,
        EnrichmentHashTags = request.EnrichmentHashTags,
        HashTag = request.HashTag,
        RedditFlairTemplateId = request.RedditFlairTemplateId,
        RedditFlareText = request.RedditFlareText,
        SubjectType = request.SubjectType,
        KnownTerms = request.KnownTerms
    };

    public static Models.FilmChangeRequest ToModel(this FilmChangeRequest request) => new()
    {
        Imdb = request.Imdb
    };

    public static Models.TvShowChangeRequest ToModel(this TvShowChangeRequest request) => new()
    {
        Imdb = request.Imdb,
        Tvdb = request.Tvdb
    };

    public static Models.TvShowEpisodeChangeRequest ToModel(this TvShowEpisodeChangeRequest request) => new()
    {
        Imdb = request.Imdb,
        Tvdb = request.Tvdb
    };

    public static Models.DiscoverySubmitRequest ToModel(this DiscoverySubmitRequest request) => new()
    {
        DiscoveryResultsDocumentIds = request.DiscoveryResultsDocumentIds,
        ResultIds = request.ResultIds
    };

    public static Models.DiscoveryScheduleUpdateRequest ToModel(this DiscoveryScheduleUpdateRequest request) => new()
    {
        RunTimes = request.RunTimes,
        TimeZoneId = request.TimeZoneId,
        Enabled = request.Enabled
    };

    public static Models.SupportedLanguageAddRequest ToModel(this SupportedLanguageAddRequest request) => new()
    {
        Name = request.Name
    };

    public static Models.TitleCasingRulesAddLowerCaseTermRequest ToModel(
        this TitleCasingRulesAddLowerCaseTermRequest request) => new()
    {
        Term = request.Term
    };

    public static Models.TitleCasingRulesAddIgnoredSubjectRequest ToModel(
        this TitleCasingRulesAddIgnoredSubjectRequest request) => new()
    {
        Term = request.Term
    };

    public static Models.PushSubscription ToModel(this PushSubscription request) => new()
    {
        Endpoint = request.Endpoint,
        ExpirationTime = request.ExpirationTime,
        Keys = request.Keys.ToModel()
    };

    public static Models.PushSubscriptionKeys ToModel(this PushSubscriptionKeys keys) => new()
    {
        Auth = keys.Auth,
        P256dh = keys.P256dh
    };

    public static Models.SubmitUrlRequest ToModel(this SubmitUrlRequest request) => new()
    {
        Url = request.Url,
        PodcastId = request.PodcastId,
        PodcastName = request.PodcastName,
        PrefetchedMeta = request.PrefetchedMeta
    };

    public static Models.SubmitUrlPrepareRequest ToModel(this SubmitUrlPrepareRequest request) => new()
    {
        Url = request.Url
    };

    public static Models.SubmitUrlExtractRequest ToModel(this SubmitUrlExtractRequest request) => new()
    {
        Url = request.Url,
        Html = request.Html
    };
}

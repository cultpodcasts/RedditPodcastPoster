using RedditPodcastPoster.Models.Episodes;

namespace RedditPodcastPoster.SocialPosting.Episodes;

/// <summary>
/// Tweet and Bluesky candidates for TV episodes, news reports and films, plus their posted-state writes.
/// Kept apart from <see cref="IRecentEpisodeCandidatesProvider"/> on purpose: that cache is shared by the
/// Categoriser, thumbnail recheck and Api outgoing service, which save results as podcast episodes.
/// Items come back as <see cref="PodcastEpisode"/> projections with <see cref="PodcastEpisode.ContentKind"/> set.
/// </summary>
public interface ICataloguePlayableSocialPosts
{
    /// <summary>Active, untweeted items released in the last <c>TweetDays</c> with a postable link, newest first.</summary>
    Task<IReadOnlyList<PodcastEpisode>> GetUntweeted();

    /// <summary>Active items not yet on Bluesky, released in the last <c>BlueSkyDays</c> with a postable link, newest first.</summary>
    Task<IReadOnlyList<PodcastEpisode>> GetBlueskyReady();

    /// <summary>
    /// Sets <c>tweeted</c> on the stored TV episode, news report or film, and <c>tweetId</c> when
    /// <paramref name="tweetId"/> is given (a stored id is kept when it is null).
    /// </summary>
    Task MarkTweeted(PodcastEpisode item, string? tweetId);

    /// <summary>Sets <c>blueskyPost</c> on the stored TV episode, news report or film.</summary>
    Task MarkBlueskyPosted(PodcastEpisode item, string blueskyPostUri);
}

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
    /// Records an X post on the stored TV episode, news report or film: stores <c>tweetPost</c> when
    /// <paramref name="tweetPost"/> is given; otherwise sets the legacy <c>tweeted</c> flag
    /// (<see cref="Models.Catalogue.Playable.OldTweeted"/>). Same dual-field shape as Bluesky.
    /// </summary>
    Task MarkTweeted(PodcastEpisode item, string? tweetPost);

    /// <summary>Sets <c>blueskyPost</c> on the stored TV episode, news report or film.</summary>
    Task MarkBlueskyPosted(PodcastEpisode item, string blueskyPostUri);
}

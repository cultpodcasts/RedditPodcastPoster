namespace RedditPodcastPoster.Models.Catalogue;

/// <summary>
/// Titled production unit (Episode, TvShowEpisode, NewsReport). Film uses
/// <see cref="Publisher.Name"/> instead and does not implement this.
/// </summary>
public interface IMediaProduction
{
    string Title { get; set; }
}

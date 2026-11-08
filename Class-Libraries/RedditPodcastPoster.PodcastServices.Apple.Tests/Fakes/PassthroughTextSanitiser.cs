using System.Text.RegularExpressions;
using RedditPodcastPoster.Models.Posting;
using RedditPodcastPoster.Text.Sanitisers;

namespace RedditPodcastPoster.PodcastServices.Apple.Tests.Fakes;

/// <summary>
/// Returns descriptions and titles unchanged — enough for catalogue classifier wiring in Apple resolver tests.
/// </summary>
internal sealed class PassthroughTextSanitiser : ITextSanitiser
{
    public Task<string> SanitiseTitle(PostModel postModel) =>
        Task.FromResult(postModel.EpisodeTitle);

    public string SanitisePodcastName(PostModel postModel) => postModel.PodcastName;

    public string SanitiseDescription(PostModel postModel) => postModel.EpisodeDescription;

    public Task<string> SanitiseTitle(
        string episodeTitle,
        Regex? regex,
        string[] podcastKnownTerms,
        string[] subjectKnownTerms,
        string? language = null) =>
        Task.FromResult(episodeTitle);

    public string SanitisePodcastName(string podcastName) => podcastName;

    public string SanitiseDescription(string episodeDescription, Regex? descriptionRegex) =>
        episodeDescription;

    public string ExtractDescription(string episodeDescription, Regex? descriptionRegex) =>
        episodeDescription;

    public string ExtractDescription(
        string episodeDescription,
        string descriptionRegex,
        string? episodeTitle,
        string? podcastName) =>
        episodeDescription;
}

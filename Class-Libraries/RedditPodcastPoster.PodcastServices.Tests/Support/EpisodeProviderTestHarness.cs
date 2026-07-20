using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RedditPodcastPoster.Common.Episodes;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Abstractions.Handlers;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;

namespace RedditPodcastPoster.PodcastServices.Tests.Support;

internal sealed class EpisodeProviderTestHarness
{
    public EpisodeProviderTestHarness()
    {
        YouTubeHandler
            .Setup(x => x.GetEpisodes(
                It.IsAny<Podcast>(),
                It.IsAny<IEnumerable<Episode>>(),
                It.IsAny<IndexingContext>()))
            .ReturnsAsync(new EpisodeRetrievalHandlerResponse([], Handled: false));

        SpotifyHandler
            .Setup(x => x.GetEpisodes(It.IsAny<Podcast>(), It.IsAny<IndexingContext>()))
            .ReturnsAsync(new EpisodeRetrievalHandlerResponse([], Handled: false));

        AppleHandler
            .Setup(x => x.GetEpisodes(It.IsAny<Podcast>(), It.IsAny<IndexingContext>()))
            .ReturnsAsync(new EpisodeRetrievalHandlerResponse([], Handled: false));
    }

    public Mock<IAppleEpisodeRetrievalHandler> AppleHandler { get; } = new();

    public Mock<IYouTubeEpisodeRetrievalHandler> YouTubeHandler { get; } = new();

    public Mock<ISpotifyEpisodeRetrievalHandler> SpotifyHandler { get; } = new();

    public Mock<IFoundEpisodeFilter> FoundEpisodeFilter { get; } = new();

    public EpisodeProvider CreateSut() =>
        new(
            AppleHandler.Object,
            YouTubeHandler.Object,
            SpotifyHandler.Object,
            FoundEpisodeFilter.Object,
            NullLogger<EpisodeProvider>.Instance);
}

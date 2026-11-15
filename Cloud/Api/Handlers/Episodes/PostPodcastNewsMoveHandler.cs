using Microsoft.Azure.Functions.Worker.Http;
using Api.Models;
using Api.Services.Episodes;

namespace Api.Handlers.Episodes;

/// <summary>POST podcast/{podcastId}/news (batch).</summary>
public class PostPodcastNewsMoveHandler(IEpisodeNewsMoveService episodeNewsMoveService) : IPostPodcastNewsMoveHandler
{
    public async Task<HttpResponseData> Handle(
        IHandlerContext ctx,
        PodcastNewsMoveCommand command,
        CancellationToken cancellationToken) =>
        await EpisodeNewsMoveResponses.Respond(
            ctx, await episodeNewsMoveService.MoveManyAsync(command, cancellationToken), cancellationToken);
}

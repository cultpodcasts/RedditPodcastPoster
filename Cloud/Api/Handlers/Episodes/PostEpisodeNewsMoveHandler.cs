using Microsoft.Azure.Functions.Worker.Http;
using Api.Models;
using Api.Services.Episodes;

namespace Api.Handlers.Episodes;

/// <summary>POST episode/{podcastId}/{episodeId}/news.</summary>
public class PostEpisodeNewsMoveHandler(IEpisodeNewsMoveService episodeNewsMoveService) : IPostEpisodeNewsMoveHandler
{
    public async Task<HttpResponseData> Handle(
        IHandlerContext ctx,
        EpisodeNewsMoveCommand command,
        CancellationToken cancellationToken) =>
        await EpisodeNewsMoveResponses.Respond(
            ctx, await episodeNewsMoveService.MoveAsync(command, cancellationToken), cancellationToken);
}

using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Api.Dtos;
using Api.Models;
using Api.Services.Discovery;

namespace Api.Handlers.Discovery;

public interface IPostDiscoverySubmitOutcomeHandler
{
    Task<HttpResponseData> Handle(IHandlerContext ctx, DiscoverySubmitOutcomeRequest model, CancellationToken c);
}

public class PostDiscoverySubmitOutcomeHandler(
    IDiscoveryResultsService discoveryResultsService,
    ILogger<PostDiscoverySubmitOutcomeHandler> logger) : IPostDiscoverySubmitOutcomeHandler
{
    public async Task<HttpResponseData> Handle(
        IHandlerContext ctx,
        DiscoverySubmitOutcomeRequest model,
        CancellationToken c)
    {
        if (model.DiscoveryResultsDocumentIds.Length == 0 || model.ResultIds.Length == 0)
        {
            return await ctx.BadRequest(new DiscoveryErrorResponse
            {
                Message = "ids and resultIds are required."
            }, c);
        }

        var items = await discoveryResultsService.GetSubmitOutcome(
            model.DiscoveryResultsDocumentIds, model.ResultIds, c);

        logger.LogInformation(
            "Discovery submit-outcome: {DocumentCount} documents, {ResultCount} results ({Accepted} accepted, {Errored} errored, {Pending} pending).",
            model.DiscoveryResultsDocumentIds.Length,
            items.Count,
            items.Count(x => x.State == "Accepted"),
            items.Count(x => x.State == "AcceptError"),
            items.Count(x => x.State is "Pending" or "Unknown"));

        return await ctx.Ok(new DiscoverySubmitOutcomeResponse
        {
            Results = items.Select(ToDto).ToArray()
        }, c);
    }

    private static DiscoverySubmitOutcomeItemDto ToDto(DiscoverySubmitOutcomeItem item) => new()
    {
        DiscoveryItemId = item.DiscoveryItemId,
        State = item.State,
        PodcastId = item.PodcastId,
        EpisodeId = item.EpisodeId,
        Message = item.Message,
        EpisodeName = item.EpisodeName,
        ShowName = item.ShowName
    };
}

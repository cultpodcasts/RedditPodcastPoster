using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Api.Dtos;
using Api.Dtos.Mapping;
using Api.Models;
using Api.Services.Episodes;
using RedditPodcastPoster.Subjects.Providers;

namespace Api.Handlers.Episodes;

public class GetOutgoingEpisodesHandler(
    IEpisodeOutgoingService episodeOutgoingService,
    ICachedSubjectProvider subjectsProvider,
    EpisodeDiscreteMapper episodeDiscreteMapper,
    ILogger<GetOutgoingEpisodesHandler> logger) : IGetOutgoingEpisodesHandler
{
    public async Task<HttpResponseData> Handle(
        IHandlerContext ctx,
        CancellationToken c)
    {
        var query = OutgoingEpisodesQuery.Parse(
            ctx.Query("days"),
            ctx.Query("posted"),
            ctx.Query("tweeted"),
            ctx.Query("blueskyPosted"));

        var result = await episodeOutgoingService.GetOutgoingAsync(query, c);

        if (result.Status == EpisodeOutgoingStatus.Failed)
        {
            return await ctx.InternalError(ApiErrorResponse.Failure("Unable to retrieve out-going episodes"), c);
        }

        if (result.Status != EpisodeOutgoingStatus.Ok)
        {
            return await LogAndFail(ctx, c);
        }

        var subjects = await subjectsProvider.GetAll().ToListAsync(c);
        var episodes = new List<DiscreteEpisode>();
        foreach (var pair in result.Episodes!)
        {
            episodes.Add(await episodeDiscreteMapper.ToDiscreteEpisode(pair.Episode, pair.Podcast, subjects));
        }

        return await ctx.Ok(episodes, c);
    }

    private async Task<HttpResponseData> LogAndFail(IHandlerContext ctx, CancellationToken c)
    {
        logger.LogError("Outgoing episodes get failed with unexpected status.");
        return await ctx.InternalError(ApiErrorResponse.Failure("Unable to retrieve out-going episodes"), c);
    }
}

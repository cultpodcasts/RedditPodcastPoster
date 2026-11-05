using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Api.Dtos;
using Api.Dtos.Extensions;
using Api.Dtos.Mapping;
using Api.Models;
using Api.Services.News;

namespace Api.Handlers.News;

public class GetNewsReportHandler(
    INewsReportService newsReportService,
    IPlayableGuestDtoEnricher guestDtoEnricher,
    ILogger<GetNewsReportHandler> logger) : IGetNewsReportHandler
{
    public async Task<HttpResponseData> Handle(
        IHandlerContext ctx,
        Guid reportId,
        CancellationToken c)
    {
        var result = await newsReportService.GetAsync(reportId, c);

        return result.Status switch
        {
            NewsReportGetStatus.Found =>
                await ctx.Ok(await ToEnrichedDto(result.Report!, c), c),
            NewsReportGetStatus.NotFound =>
                await ctx.NotFound(ApiErrorResponse.Failure("Unable to retrieve news report"), c),
            NewsReportGetStatus.Failed =>
                await ctx.InternalError(ApiErrorResponse.Failure("Unable to retrieve news report"), c),
            _ => await LogAndFail(ctx, c)
        };
    }

    private async Task<NewsReportDto> ToEnrichedDto(
        RedditPodcastPoster.Models.News.NewsReport report,
        CancellationToken c)
    {
        var dto = report.ToDto();
        await guestDtoEnricher.EnrichAsync(
            dto, report.Title, report.Description, report.Language, report.Guests, c);
        return dto;
    }

    private async Task<HttpResponseData> LogAndFail(IHandlerContext ctx, CancellationToken c)
    {
        logger.LogError("News report get failed with unexpected status.");
        return await ctx.InternalError(ApiErrorResponse.Failure("Unable to retrieve news report"), c);
    }
}

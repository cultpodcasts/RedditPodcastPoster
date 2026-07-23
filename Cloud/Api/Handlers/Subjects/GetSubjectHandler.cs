using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Api.Dtos;
using Api.Dtos.Extensions;
using Api.Models;
using Api.Services.Subjects;

namespace Api.Handlers.Subjects;

public class GetSubjectHandler(
    ISubjectGetService subjectGetService,
    ILogger<GetSubjectHandler> logger) : IGetSubjectHandler
{
    public async Task<HttpResponseData> Handle(
        IHandlerContext ctx,
        string subjectName,
        CancellationToken c)
    {
        var result = await subjectGetService.GetAsync(subjectName, c);

        return result.Status switch
        {
            SubjectGetStatus.Ok =>
                await ctx.Ok(result.Subject!.ToDto(), c),
            SubjectGetStatus.NotFound =>
                ctx.NotFound(),
            SubjectGetStatus.Failed =>
                await ctx.InternalError(ApiErrorResponse.Failure("Unable to retrieve subject"), c),
            _ => await LogAndFail(ctx, c)
        };
    }

    private async Task<HttpResponseData> LogAndFail(IHandlerContext ctx, CancellationToken c)
    {
        logger.LogError("Subject get failed with unexpected status.");
        return await ctx.InternalError(ApiErrorResponse.Failure("Unable to retrieve subject"), c);
    }
}

using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Api.Dtos;
using Api.Models;
using Api.Services.Subjects;

namespace Api.Handlers.Subjects;

public class PostSubjectHandler(
    ISubjectUpdateService subjectUpdateService,
    ILogger<PostSubjectHandler> logger) : IPostSubjectHandler
{
    public async Task<HttpResponseData> Handle(
        IHandlerContext ctx,
        SubjectChangeRequestWrapper subjectChangeRequestWrapper,
        CancellationToken c)
    {
        var result = await subjectUpdateService.UpdateAsync(subjectChangeRequestWrapper, c);

        return result.Status switch
        {
            SubjectUpdateStatus.Accepted =>
                ctx.Accepted(),
            SubjectUpdateStatus.NotFound =>
                ctx.NotFound(),
            SubjectUpdateStatus.Failed =>
                await ctx.InternalError(ApiErrorResponse.Failure("Unable to update subject"), c),
            _ => await LogAndFail(ctx, c)
        };
    }

    private async Task<HttpResponseData> LogAndFail(IHandlerContext ctx, CancellationToken c)
    {
        logger.LogError("Subject update failed with unexpected status.");
        return await ctx.InternalError(ApiErrorResponse.Failure("Unable to update subject"), c);
    }
}

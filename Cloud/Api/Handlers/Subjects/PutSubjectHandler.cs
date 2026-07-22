using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Api.Dtos;
using Api.Dtos.Extensions;
using Api.Models;
using Api.Services.Subjects;

namespace Api.Handlers.Subjects;

public class PutSubjectHandler(
    ISubjectCreateService subjectCreateService,
    ILogger<PutSubjectHandler> logger) : IPutSubjectHandler
{
    public async Task<HttpResponseData> Handle(
        IHandlerContext ctx,
        SubjectChangeRequest subject,
        CancellationToken ct)
    {
        var result = await subjectCreateService.CreateAsync(subject, ct);

        return result.Status switch
        {
            SubjectCreateStatus.Accepted =>
                await ctx.Accepted(result.Subject!.ToDto(), ct),
            SubjectCreateStatus.BadRequest =>
                await ctx.BadRequest(new { message = result.Message }, ct),
            SubjectCreateStatus.Conflict =>
                await ctx.Conflict(new { conflict = result.ConflictName }, ct),
            SubjectCreateStatus.Failed =>
                await ctx.InternalError(ApiErrorResponse.Failure("Unable to create subject"), ct),
            _ => await LogAndFail(ctx, ct)
        };
    }

    private async Task<HttpResponseData> LogAndFail(IHandlerContext ctx, CancellationToken ct)
    {
        logger.LogError("Subject create failed with unexpected status.");
        return await ctx.InternalError(ApiErrorResponse.Failure("Unable to create subject"), ct);
    }
}

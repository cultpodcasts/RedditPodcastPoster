using Microsoft.Azure.Functions.Worker.Http;
using Api.Dtos;
using Api.Models;
using Api.Services.News;

namespace Api.Handlers.News;

/// <summary>
/// DELETE newsorganisation/{id}[?redirectToNewsOrganisationId={guid}] (admin): 200 deleted (empty body), 404 not found
/// (empty body), 400 for a bad or unsafe redirect target, 409 with <c>remainingReportCount</c> while it still has
/// reports or with <c>message</c> and <c>retryable: true</c> while it is being indexed (nothing deleted), 500 failed.
/// </summary>
public class DeleteNewsOrganisationHandler(INewsOrganisationDeleteService deleteService) : IDeleteNewsOrganisationHandler
{
    public const string RedirectQueryParameter = "redirectToNewsOrganisationId";

    public async Task<HttpResponseData> Handle(IHandlerContext ctx, Guid newsOrganisationId, CancellationToken c)
    {
        Guid? redirectTo = null;
        var raw = ctx.Query(RedirectQueryParameter);
        if (!string.IsNullOrWhiteSpace(raw))
        {
            if (!Guid.TryParse(raw, out var parsed) || parsed == Guid.Empty)
            {
                return await ctx.BadRequest(ApiErrorResponse.Failure($"{RedirectQueryParameter} must be a guid"), c);
            }

            redirectTo = parsed;
        }

        var result = await deleteService.DeleteAsync(newsOrganisationId, redirectTo, c);
        return result.Status switch
        {
            NewsOrganisationDeleteStatus.Deleted => ctx.Ok(),
            NewsOrganisationDeleteStatus.NotFound => ctx.NotFound(),
            NewsOrganisationDeleteStatus.BadRequest => await ctx.BadRequest(
                ApiErrorResponse.Failure(result.Error ?? "Invalid request"), c),
            NewsOrganisationDeleteStatus.HasReports => await ctx.Conflict(new
            {
                message = "The news organisation still has reports. Move them first",
                remainingReportCount = result.RemainingReportCount
            }, c),
            NewsOrganisationDeleteStatus.Busy => await ctx.Conflict(new
            {
                message = "The news organisation is being indexed. Retry in a few minutes",
                retryable = true
            }, c),
            _ => await ctx.InternalError(ApiErrorResponse.Failure("Unable to delete news organisation"), c)
        };
    }
}

using Microsoft.Azure.Functions.Worker.Http;
using Api.Dtos;
using Api.Models;
using Api.Services.News;

namespace Api.Handlers.News;

/// <summary>
/// DELETE newsorganisation/{id} (admin): 200 deleted, 404 not found, 409 with <c>remainingReportCount</c> while it still
/// has reports (nothing deleted), 500 failed.
/// </summary>
public class DeleteNewsOrganisationHandler(INewsOrganisationDeleteService deleteService) : IDeleteNewsOrganisationHandler
{
    public async Task<HttpResponseData> Handle(IHandlerContext ctx, Guid newsOrganisationId, CancellationToken c)
    {
        var result = await deleteService.DeleteAsync(newsOrganisationId, c);
        return result.Status switch
        {
            NewsOrganisationDeleteStatus.Deleted => ctx.Ok(),
            NewsOrganisationDeleteStatus.NotFound => ctx.NotFound(),
            NewsOrganisationDeleteStatus.HasReports => await ctx.Conflict(new
            {
                message = "The news organisation still has reports. Move them first",
                remainingReportCount = result.RemainingReportCount
            }, c),
            _ => await ctx.InternalError(ApiErrorResponse.Failure("Unable to delete news organisation"), c)
        };
    }
}

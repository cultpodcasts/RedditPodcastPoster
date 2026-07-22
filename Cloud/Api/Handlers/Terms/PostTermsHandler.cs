using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Api.Models;
using Api.Services.Terms;

namespace Api.Handlers.Terms;

public class PostTermsHandler(
    ITermsSubmitService termsSubmitService,
    ILogger<PostTermsHandler> logger) : IPostTermsHandler
{
    public async Task<HttpResponseData> Handle(
        IHandlerContext ctx,
        TermSubmitRequest termSubmitRequest,
        CancellationToken c)
    {
        var result = await termsSubmitService.SubmitAsync(termSubmitRequest, c);
        return result.Status switch
        {
            TermsSubmitStatus.Ok =>
                await ctx.Ok(new { }, c),
            TermsSubmitStatus.Conflict =>
                ctx.Conflict(),
            TermsSubmitStatus.Failed =>
                ctx.InternalError(),
            _ => LogAndFail(ctx)
        };
    }

    private HttpResponseData LogAndFail(IHandlerContext ctx)
    {
        logger.LogError("Terms submit failed with unexpected status.");
        return ctx.InternalError();
    }
}

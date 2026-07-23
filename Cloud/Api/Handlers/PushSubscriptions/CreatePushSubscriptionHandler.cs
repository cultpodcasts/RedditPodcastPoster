using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Api.Models;
using Api.Services.PushSubscriptions;

namespace Api.Handlers.PushSubscriptions;

public class CreatePushSubscriptionHandler(
    IPushSubscriptionCreateService pushSubscriptionCreateService,
    ILogger<CreatePushSubscriptionHandler> logger) : ICreatePushSubscriptionHandler
{
    public async Task<HttpResponseData> Handle(
        IHandlerContext ctx,
        PushSubscription pushSubscription,
        CancellationToken c)
    {
        var result = await pushSubscriptionCreateService.CreateAsync(pushSubscription, ctx.Subject, c);
        return result.Status switch
        {
            PushSubscriptionCreateStatus.Created =>
                ctx.Ok(),
            PushSubscriptionCreateStatus.NoUser or PushSubscriptionCreateStatus.Failed =>
                ctx.InternalError(),
            _ => LogAndFail(ctx)
        };
    }

    private HttpResponseData LogAndFail(IHandlerContext ctx)
    {
        logger.LogError("Push subscription create failed with unexpected status.");
        return ctx.InternalError();
    }
}

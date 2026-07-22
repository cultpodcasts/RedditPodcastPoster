using Microsoft.Azure.Functions.Worker.Http;
using Api.Models;

namespace Api.Handlers.PushSubscriptions;

public interface ICreatePushSubscriptionHandler
{
    Task<HttpResponseData> Handle(
        IHandlerContext ctx,
        PushSubscription pushSubscription,
        CancellationToken c);
}

namespace Api.Models;

public class PushSubscription
{
    public required Uri Endpoint { get; set; }

    public long? ExpirationTime { get; set; }

    public required PushSubscriptionKeys Keys { get; set; }
}

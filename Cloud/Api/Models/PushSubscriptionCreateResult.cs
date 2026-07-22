namespace Api.Models;

public enum PushSubscriptionCreateStatus
{
    Created,
    NoUser,
    Failed
}

public record PushSubscriptionCreateResult(PushSubscriptionCreateStatus Status);

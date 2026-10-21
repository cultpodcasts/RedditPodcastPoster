namespace Api.Models;

public class PushSubscriptionKeys
{
    public required string Auth { get; set; }

    public required string P256dh { get; set; }
}

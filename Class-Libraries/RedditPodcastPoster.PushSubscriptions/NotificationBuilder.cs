using RedditPodcastPoster.PushSubscriptions.Dtos;

namespace RedditPodcastPoster.PushSubscriptions;

public class NotificationBuilder
{
    private readonly Payload _payload = new();

    public NotificationBuilder WithTitle(string title)
    {
        _payload.Notification ??= new Notification();
        _payload.Notification.Title = title;
        return this;
    }

    public NotificationBuilder WithBody(string body)
    {
        _payload.Notification ??= new Notification();
        _payload.Notification.Body = body;
        return this;
    }

    public NotificationBuilder WithIcon(string icon)
    {
        _payload.Notification ??= new Notification();
        _payload.Notification.Icon = icon;
        return this;
    }

    public NotificationBuilder WithAction(string title, string action, string? icon = null)
    {
        _payload.Notification ??= new Notification();
        _payload.Notification.Actions ??= [];
        _payload.Notification.Actions.Add(new NotificationAction {Action = action, Title = title, Icon = icon});
        return this;
    }

    public NotificationBuilder WithBadge(string badge)
    {
        _payload.Notification ??= new Notification();
        _payload.Notification.Badge = badge;
        return this;
    }

    public NotificationBuilder WithImage(string image)
    {
        _payload.Notification ??= new Notification();
        _payload.Notification.Image = image;
        return this;
    }

    public NotificationBuilder WithRenotify(bool renotify)
    {
        _payload.Notification ??= new Notification();
        _payload.Notification.Renotify = renotify;
        return this;
    }

    public NotificationBuilder WithRequireInteraction(bool requireInteraction)
    {
        _payload.Notification ??= new Notification();
        _payload.Notification.RequireInteraction = requireInteraction;
        return this;
    }

    public NotificationBuilder WithSilent(bool silent)
    {
        _payload.Notification ??= new Notification();
        _payload.Notification.Silent = silent;
        return this;
    }

    public NotificationBuilder WithTag(string tag)
    {
        _payload.Notification ??= new Notification();
        _payload.Notification.Tag = tag;
        return this;
    }

    public NotificationBuilder WithTimestamp(DateTimeOffset timestamp)
    {
        _payload.Notification ??= new Notification();
        _payload.Notification.Timestamp = timestamp.ToUnixTimeMilliseconds();
        return this;
    }

    public Payload Build()
    {
        return _payload;
    }

    public NotificationBuilder WithVibrate(int[] vibrate)
    {
        _payload.Notification ??= new Notification();
        _payload.Notification.Vibrate = vibrate;
        return this;
    }

    public NotificationBuilder WithData(object data)
    {
        _payload.Notification ??= new Notification();
        _payload.Notification.Data = data;
        return this;
    }
}
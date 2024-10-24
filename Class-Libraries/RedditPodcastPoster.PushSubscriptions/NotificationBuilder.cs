using RedditPodcastPoster.PushSubscriptions.Dtos;

namespace RedditPodcastPoster.PushSubscriptions;

public class NotificationBuilder
{
    private readonly Payload _payload;

    public NotificationBuilder(string title, string body)
    {
        _payload = new Payload();
        WithTitle(title);
        WithBody(body);
    }

    public NotificationBuilder()
    {
        _payload = new Payload();
    }

    public NotificationBuilder WithTitle(string title)
    {
        if (_payload.Notification == null)
        {
            _payload.Notification = new Notification();
        }

        _payload.Notification.Title = title;
        return this;
    }

    public NotificationBuilder WithBody(string body)
    {
        if (_payload.Notification == null)
        {
            _payload.Notification = new Notification();
        }

        _payload.Notification.Body = body;
        return this;
    }

    public Payload Build()
    {
        return _payload;
    }
}
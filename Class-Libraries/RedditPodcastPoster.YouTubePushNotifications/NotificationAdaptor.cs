using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.YouTubePushNotifications.Models;

namespace RedditPodcastPoster.YouTubePushNotifications;

public class NotificationAdaptor(ILogger<NotificationAdaptor> logger) : INotificationAdaptor
{
    private static readonly XNamespace Namespace = "http://www.w3.org/2005/Atom";
    private static readonly XNamespace YouTube = "http://www.youtube.com/xml/schemas/2015";
    private static readonly XNamespace Media = "http://search.yahoo.com/mrss/";

    public Notification Adapt(XDocument xml)
    {
        var root = xml.Root;

        var linkAlternative = xml.Root!.Elements(Namespace + "link")
            .SingleOrDefault(x => x.Attribute("rel")?.Value == "alternate");
        var notification = new Notification
        {
            Id = xml.Root.Element(Namespace + "id")?.Value ?? string.Empty,
            ChannelId = xml.Root.Element(YouTube + "channelId")?.Value ?? string.Empty,
            Title = xml.Root.Element(Namespace + "title")?.Value ?? string.Empty,
            LinkAlternative = linkAlternative != null && linkAlternative.Attribute("href") != null
                ? new Uri(linkAlternative.Attribute("href")!.Value)
                : null,
            AuthorName = xml.Root.Element(Namespace + "author")?.Element(Namespace + "name")?.Value ?? string.Empty,
            AuthorUri = xml.Root.Element(Namespace + "author")?.Element(Namespace + "uri") != null
                ? new Uri(xml.Root.Element(Namespace + "author")!.Element(Namespace + "uri")!.Value, UriKind.Absolute)
                : null,
            Published = xml.Root.Element(Namespace + "published") != null
                ? DateTime.Parse(xml.Root.Element(Namespace + "published")!.Value)
                : null
        };
        var entries = xml.Root.Elements(Namespace + "entry");
        notification.Entries = entries.Select(x => CreateEntry(x));

        return notification;
    }

    private Notification.EntryEntity CreateEntry(XElement entry)
    {
        var linkAlternative = entry.Elements(Namespace + "link")
            .SingleOrDefault(x => x.Attribute("rel")?.Value == "alternate");
        var entryEntity = new Notification.EntryEntity
        {
            Id = entry.Element(Namespace + "id")?.Value ?? string.Empty,
            VideoId = entry.Element(YouTube + "videoId")?.Value ?? string.Empty,
            ChannelId = entry.Element(YouTube + "channelId")?.Value ?? string.Empty,
            Title = entry.Element(Namespace + "title")?.Value ?? string.Empty,
            LinkAlternative = linkAlternative != null && linkAlternative.Attribute("href") != null
                ? new Uri(linkAlternative.Attribute("href")!.Value)
                : null,
            AuthorName = entry.Element(Namespace + "author")?.Element(Namespace + "name")?.Value ?? string.Empty,
            AuthorUri = entry.Element(Namespace + "author")?.Element(Namespace + "uri") != null
                ? new Uri(entry.Element(Namespace + "author")!.Element(Namespace + "uri")!.Value,
                    UriKind.Absolute)
                : null,
            Published = entry.Element(Namespace + "published") != null
                ? DateTime.Parse(entry.Element(Namespace + "published")!.Value)
                : null,
            Updated = entry.Element(Namespace + "updated") != null
                ? DateTime.Parse(entry.Element(Namespace + "updated")!.Value)
                : null
        };

        var group = entry.Element(Media + "group");
        if (group != null)
        {
            var content = group.Element(Media + "content");
            var thumbnail = group.Element(Media + "thumbnail");
            var community = group.Element(Media + "community");
            entryEntity.Group = new Notification.EntryEntity.MediaGroup
            {
                Title = group.Element(Media + "title")?.Value ?? string.Empty,
                ContentUrl = content?.Attribute("url") != null
                    ? new Uri(content.Attribute("url")!.Value, UriKind.Absolute)
                    : null,
                ContentWidth = content?.Attribute("width") != null &&
                               int.TryParse(content?.Attribute("width")!.Value, out var contentWidth)
                    ? contentWidth
                    : null,
                ContentHeight = content?.Attribute("height") != null &&
                                int.TryParse(content?.Attribute("height")!.Value, out var contentHeight)
                    ? contentHeight
                    : null,
                ThumbnailUrl = thumbnail?.Attribute("url") != null
                    ? new Uri(thumbnail.Attribute("url")!.Value, UriKind.Absolute)
                    : null,
                ThumbnailWidth = thumbnail?.Attribute("width") != null &&
                                 int.TryParse(thumbnail?.Attribute("width")!.Value, out var thumbnailWidth)
                    ? thumbnailWidth
                    : null,
                ThumbnailHeight = thumbnail?.Attribute("height") != null &&
                                  int.TryParse(thumbnail?.Attribute("height")!.Value, out var thumbnailHeight)
                    ? thumbnailHeight
                    : null,
                Description = group.Element(Media + "description")?.Value ?? string.Empty
            };
            if (community != null)
            {
                var starRating = community.Element(Media + "starRating");
                var statistics = community.Element(Media + "statistics");
                entryEntity.Group.Community = new Notification.EntryEntity.MediaGroup.MediaCommunity
                {
                    StarRatingCount = starRating?.Attribute("count") != null &&
                                      int.TryParse(starRating!.Attribute("count")!.Value,
                                          out var starRatingCount)
                        ? starRatingCount
                        : null,
                    StarRatingAverage = starRating?.Attribute("average") != null &&
                                        decimal.TryParse(starRating!.Attribute("average")!.Value,
                                            out var starRatingAverage)
                        ? starRatingAverage
                        : null,
                    StarRatingMin = starRating?.Attribute("min") != null &&
                                    int.TryParse(starRating!.Attribute("min")!.Value,
                                        out var starRatingMin)
                        ? starRatingMin
                        : null,
                    StarRatingMax = starRating?.Attribute("max") != null &&
                                    int.TryParse(starRating!.Attribute("max")!.Value,
                                        out var starRatingMax)
                        ? starRatingMax
                        : null,
                    StatisticsViews = statistics?.Attribute("views") != null &&
                                      int.TryParse(statistics!.Attribute("views")!.Value,
                                          out var statisticsViews)
                        ? statisticsViews
                        : null
                };
            }
        }

        return entryEntity;
    }
}
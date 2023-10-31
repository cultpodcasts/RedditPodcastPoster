namespace RedditPodcastPoster.YouTubePushNotifications.Models;

public class Notification
{
    public required string Id { get; set; }
    public required string ChannelId { get; set; }
    public required string Title { get; set; }
    public Uri? LinkAlternative { get; set; }
    public required string AuthorName { get; set; }
    public Uri? AuthorUri { get; set; }
    public DateTime? Published { get; set; }

    public IEnumerable<EntryEntity> Entries { get; set; } = new List<EntryEntity>();

    public class EntryEntity
    {
        public required string Id { get; set; }
        public required string VideoId { get; set; }
        public required string ChannelId { get; set; }
        public required string Title { get; set; }
        public Uri? LinkAlternative { get; set; }
        public required string AuthorName { get; set; }
        public Uri? AuthorUri { get; set; }
        public DateTime? Published { get; set; }
        public DateTime? Updated { get; set; }
        public MediaGroup? Group { get; set; }

        public class MediaGroup
        {
            public required string Title { get; set; }
            public Uri? ContentUrl { get; set; }
            public int? ContentWidth { get; set; }
            public int? ContentHeight { get; set; }
            public Uri? ThumbnailUrl { get; set; }
            public int? ThumbnailWidth { get; set; }
            public int? ThumbnailHeight { get; set; }
            public required string Description { get; set; }

            public MediaCommunity? Community { get; set; }

            public class MediaCommunity
            {
                public int? StarRatingCount { get; set; }
                public decimal? StarRatingAverage { get; set; }
                public int? StarRatingMin { get; set; }
                public int? StarRatingMax { get; set; }
                public long? StatisticsViews { get; set; }
            }
        }
    }
}
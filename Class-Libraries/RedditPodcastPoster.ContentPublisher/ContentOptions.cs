namespace RedditPodcastPoster.ContentPublisher
{
    public class ContentOptions
    {
        public required string BucketName { get; set; }
        public required string HomepageKey { get; set; }
        public required string PreProcessedHomepageKey { get; set; }
        public required string SubjectsKey { get; set; }
        public required string FlairsKey { get; set; }
    }
}

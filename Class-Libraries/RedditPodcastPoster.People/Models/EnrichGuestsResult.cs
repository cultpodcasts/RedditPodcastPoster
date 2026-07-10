namespace RedditPodcastPoster.People.Models;

public record EnrichGuestsResult(
    string[] Additions,
    PersonMatch[] SkippedLowConfidence);

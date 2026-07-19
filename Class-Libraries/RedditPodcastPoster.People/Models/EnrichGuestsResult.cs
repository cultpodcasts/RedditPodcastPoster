namespace RedditPodcastPoster.People.Models;

public record EnrichGuestsResult(
    PersonMatch[] Additions,
    PersonMatch[] SkippedLowConfidence);

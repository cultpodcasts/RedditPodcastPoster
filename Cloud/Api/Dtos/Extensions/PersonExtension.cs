namespace Api.Dtos.Extensions;

public static class PersonExtension
{
    public static Person ToDto(this RedditPodcastPoster.Models.Person person)
    {
        return new Person
        {
            Id = person.Id,
            Name = person.Name,
            Aliases = person.Aliases,
            TwitterHandle = person.TwitterHandle,
            BlueskyHandle = person.BlueskyHandle
        };
    }
}

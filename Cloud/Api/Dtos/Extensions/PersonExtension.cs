
using RedditPodcastPoster.People.Models;

namespace Api.Dtos.Extensions;

public static class PersonExtension
{
    public static PersonDto ToDto(this RedditPodcastPoster.Models.People.Person person)
    {
        return new PersonDto
        {
            Id = person.Id,
            Name = person.Name,
            SortName = person.SortName,
            IsOrganization = person.IsOrganization,
            Aliases = person.Aliases,
            TwitterHandle = person.TwitterHandle,
            BlueskyHandle = person.BlueskyHandle
        };
    }

    public static PersonDto ToDto(this PersonMatchPerson person)
    {
        return new PersonDto
        {
            Id = person.Id,
            Name = person.Name,
            SortName = person.SortName,
            IsOrganization = person.IsOrganization,
            Aliases = person.Aliases,
            TwitterHandle = person.TwitterHandle,
            BlueskyHandle = person.BlueskyHandle
        };
    }
}

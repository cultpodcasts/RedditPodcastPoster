namespace RedditPodcastPoster.Subjects;

public class RecycledFlareIdProvider : IRecycledFlareIdProvider
{
    private readonly Dictionary<string, Guid> FlareIds = new()
    {
        {"red-b-white", Guid.Parse("273d0e32-2547-11ed-a493-2afd8e18de7c")},
        {"pink-b-white", Guid.Parse("de0ca0ce-304f-11ed-ac3b-da4031bd66f5")},
        {"mandarin-d-black", Guid.Parse("ffe7e9ae-3060-11ed-a78d-960987c2a093")},
        {"navyblue-d-white", Guid.Parse("f9a96c40-3284-11ed-ba49-8ef0a808c862")}
    };

    public Guid GetId(string key)
    {
        if (!FlareIds.TryGetValue(key, out var id))
        {
            throw new ArgumentException("Unknown flare-key", nameof(key));
        }

        return id;
    }

    public string[] GetKeys()
    {
        return FlareIds.Keys.ToArray();
    }
}
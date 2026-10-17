namespace CultPodcasts.DatabasePublisher.Consistency;

/// <summary>Whether a write may only create a file that is not there, or only replace one that is.</summary>
public enum WriteMode
{
    Create,
    Replace
}

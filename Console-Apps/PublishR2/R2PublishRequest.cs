namespace PublishR2;

public class R2PublishRequest
{
    public required R2PublishTarget Target { get; init; }
}

public enum R2PublishTarget
{
    Languages,
    People,
    All
}

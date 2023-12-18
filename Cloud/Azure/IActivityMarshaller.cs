namespace Azure;

public interface IActivityMarshaller
{
    Task<ActivityStatus> Initiate(Guid id, string operationType);
    Task<ActivityStatus> Complete(Guid id, string operationType);
}
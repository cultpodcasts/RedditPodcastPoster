using System.Net;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Scripts;
using Microsoft.Extensions.Logging;

namespace Azure;

public class ActivityMarshaller : IActivityMarshaller
{
    private readonly Container _container;
    private readonly ILogger<ActivityMarshaller> _logger;

    public ActivityMarshaller(
        Container container,
        ILogger<ActivityMarshaller> logger)
    {
        _container = container;
        _logger = logger;
    }

    public async Task<ActivityStatus> Initiate(Guid id, string operationType)
    {
        try
        {
            dynamic activity = new {Id = id, Status = "initiate", OperationType = operationType};
            var result = await _container.Scripts.ExecuteStoredProcedureAsync<Activity>(
                "bookActivity",
                new PartitionKey("Activity"),
                new[] {activity});
            if (result.StatusCode == HttpStatusCode.OK && result.Resource.Status == "initiate")
            {
                return ActivityStatus.Initiated;
            }

            return ActivityStatus.Failed;
        }
        catch (CosmosException ex)
        {
            if (ex.StatusCode == HttpStatusCode.BadRequest && ex.Message.Contains("Activity Already Complete"))
            {
                _logger.LogInformation("Activity is already complete.");
                return ActivityStatus.Completed;
            }

            return ActivityStatus.Failed;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                $"Failure to initialise activity with id '{id}' for operation-type '{operationType}'.");
            return ActivityStatus.Failed;
        }
    }

    public async Task<ActivityStatus> Complete(Guid id, string operationType)
    {
        try
        {
            dynamic activity = new {Id = id, Status = "complete", OperationType = operationType};
            var result = await _container.Scripts.ExecuteStoredProcedureAsync<Activity>(
                "bookActivity",
                new PartitionKey("Activity"),
                new[] {activity},
                new StoredProcedureRequestOptions());
            if (result.StatusCode == HttpStatusCode.OK && result.Resource.Status == "complete")
            {
                return ActivityStatus.Completed;
            }

            return ActivityStatus.Failed;
        }
        catch (CosmosException ex)
        {
            if (ex.StatusCode == HttpStatusCode.BadRequest && ex.Message.Contains("Activity Already Complete"))
            {
                _logger.LogInformation("Activity is already complete.");
                return ActivityStatus.Completed;
            }

            return ActivityStatus.Failed;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                $"Failure to complete activity with id '{id}' for operation-type '{operationType}'.");
            return ActivityStatus.Failed;
        }
    }
}
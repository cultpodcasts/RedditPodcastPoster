using System.Net;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Scripts;
using Microsoft.Extensions.Logging;

namespace Azure;

public class ActivityMarshaller : IActivityMarshaller
{
    private const string ActivityBookingProcedureId = "bookActivity";
    private const string CompleteStatus = "complete";
    private const string InitiateActionStatus = "initiate";
    private const string CompletedStatusMessage = "Activity Already Complete";
    private const string InitiatedStatusMessage = "Activity Already Initiate";
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
            dynamic activity = new {Id = id, Status = InitiateActionStatus, OperationType = operationType};
            var result = await _container.Scripts.ExecuteStoredProcedureAsync<Activity>(
                ActivityBookingProcedureId,
                new PartitionKey(Activity.PartitionKey),
                new[] {activity});
            if (result.StatusCode == HttpStatusCode.OK && result.Resource.Status == InitiateActionStatus)
            {
                return ActivityStatus.Initiated;
            }

            return ActivityStatus.Failed;
        }
        catch (CosmosException ex)
        {
            if (ex.StatusCode == HttpStatusCode.BadRequest)
            {
                if (ex.Message.Contains(CompletedStatusMessage))
                {
                    _logger.LogInformation("Activity is already complete.");
                    try
                    {
                        await _container.DeleteItemAsync<Activity>(
                            id.ToString(),
                            new PartitionKey(Activity.PartitionKey));
                    }
                    catch (Exception ex2)
                    {
                        _logger.LogError(ex2, $"Failure to clean-up activity with id '{id}'.");
                    }

                    return ActivityStatus.Completed;
                }

                if (ex.Message.Contains(InitiatedStatusMessage))
                {
                    _logger.LogInformation("Activity is already initiated.");
                    return ActivityStatus.AlreadyInitiated;
                }
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
            dynamic activity = new {Id = id, Status = CompleteStatus, OperationType = operationType};
            var result = await _container.Scripts.ExecuteStoredProcedureAsync<Activity>(
                ActivityBookingProcedureId,
                new PartitionKey(Activity.PartitionKey),
                new[] {activity},
                new StoredProcedureRequestOptions());
            if (result.StatusCode == HttpStatusCode.OK && result.Resource.Status == CompleteStatus)
            {
                return ActivityStatus.Completed;
            }

            return ActivityStatus.Failed;
        }
        catch (CosmosException ex)
        {
            if (ex.StatusCode == HttpStatusCode.BadRequest)
            {
                if (ex.Message.Contains(CompletedStatusMessage))
                {
                    _logger.LogInformation("Activity is already complete.");
                    return ActivityStatus.Completed;
                }

                if (ex.Message.Contains(InitiatedStatusMessage))
                {
                    _logger.LogInformation("Activity is already initiated.");
                    return ActivityStatus.AlreadyInitiated;
                }
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
using System.Net;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Scripts;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models.Extensions;

namespace Azure;

public class ActivityMarshaller(
    Container container,
    ILogger<ActivityMarshaller> logger)
    : IActivityMarshaller
{
    private const string ActivityBookingProcedureId = "bookActivity";
    private const string CompleteStatus = "complete";
    private const string InitiateActionStatus = "initiate";
    private const string CompletedStatusMessage = "Activity Already Complete";
    private const string InitiatedStatusMessage = "Activity Already Initiate";

    public async Task<ActivityStatus> Initiate(Guid id, string operationType)
    {
        try
        {
            dynamic activity = new {Id = id, Status = InitiateActionStatus, OperationType = operationType};
            var result = await container.Scripts.ExecuteStoredProcedureAsync<Activity>(
            ActivityBookingProcedureId,
                new PartitionKey(CosmosSelectorExtensions.GetModelType<Activity>().ToString()),
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
                    logger.LogInformation("Activity is already complete.");
                    try
                    {
                        await container.DeleteItemAsync<Activity>(
                            id.ToString(),
                            new PartitionKey(CosmosSelectorExtensions.GetModelType<Activity>().ToString()));
                    }
                    catch (Exception ex2)
                    {
                        logger.LogError(ex2, $"Failure to clean-up activity with id '{id}'.");
                    }

                    return ActivityStatus.Completed;
                }

                if (ex.Message.Contains(InitiatedStatusMessage))
                {
                    logger.LogInformation("Activity is already initiated.");
                    return ActivityStatus.AlreadyInitiated;
                }
            }

            return ActivityStatus.Failed;
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                $"Failure to initialise activity with id '{id}' for operation-type '{operationType}'.");
            return ActivityStatus.Failed;
        }
    }

    public async Task<ActivityStatus> Complete(Guid id, string operationType)
    {
        try
        {
            dynamic activity = new {Id = id, Status = CompleteStatus, OperationType = operationType};
            var result = await container.Scripts.ExecuteStoredProcedureAsync<Activity>(
                ActivityBookingProcedureId,
                new PartitionKey(CosmosSelectorExtensions.GetModelType<Activity>().ToString()),
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
                    logger.LogInformation("Activity is already complete.");
                    return ActivityStatus.Completed;
                }

                if (ex.Message.Contains(InitiatedStatusMessage))
                {
                    logger.LogInformation("Activity is already initiated.");
                    return ActivityStatus.AlreadyInitiated;
                }
            }

            return ActivityStatus.Failed;
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                $"Failure to complete activity with id '{id}' for operation-type '{operationType}'.");
            return ActivityStatus.Failed;
        }
    }
}
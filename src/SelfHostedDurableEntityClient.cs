using DurableTask.AspNetCore.Extensions;
using DurableTask.Core;
using DurableTask.Core.Entities;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.DurableTask.Client.Entities;
using Microsoft.DurableTask.Entities;
using System.Diagnostics.CodeAnalysis;

namespace DurableTask.AspNetCore;

internal sealed class SelfHostedDurableEntityClient : DurableEntityClient
{
    private readonly IOrchestrationServiceClient serviceClient;
    private readonly EntityBackendQueries entityBackendQueries;
    private readonly DataConverter dataConverter;

    public SelfHostedDurableEntityClient(
        IOrchestrationServiceClient serviceClient,
        EntityBackendQueries entityBackendQueries,
        DataConverter dataConverter,
        string name) : base(name)
    {
        this.serviceClient = serviceClient;
        this.entityBackendQueries = entityBackendQueries;
        this.dataConverter = dataConverter;
    }

    public override async Task<CleanEntityStorageResult> CleanEntityStorageAsync(CleanEntityStorageRequest? request = null, bool continueUntilComplete = true, CancellationToken cancellation = default)
    {
        var result = await entityBackendQueries
            .CleanEntityStorageAsync(new EntityBackendQueries.CleanEntityStorageRequest()
            {
                RemoveEmptyEntities = request?.RemoveEmptyEntities ?? true,
                ReleaseOrphanedLocks = request?.ReleaseOrphanedLocks ?? true,
                ContinuationToken = request?.ContinuationToken,
            }, cancellation);

        return new CleanEntityStorageResult()
        {
            ContinuationToken = result.ContinuationToken,
            EmptyEntitiesRemoved = result.EmptyEntitiesRemoved,
            OrphanedLocksReleased = result.OrphanedLocksReleased,
        };
    }

    public override AsyncPageable<EntityMetadata> GetAllEntitiesAsync(EntityQuery? filter = null)
    {
        return GetAllEntitiesAsync(m => Convert(m), filter);
    }

    public override AsyncPageable<EntityMetadata<T>> GetAllEntitiesAsync<T>(EntityQuery? filter = null)
    {
        return GetAllEntitiesAsync(m => Convert<T>(m), filter);
    }

    public override async Task<EntityMetadata?> GetEntityAsync(
        EntityInstanceId id,
        bool includeState = true,
        CancellationToken cancellation = default)
    {
        var entity = await entityBackendQueries.GetEntityAsync(
             id.ToCore(), includeState, false, cancellation);

        return Convert(entity);
    }

    public override async Task<EntityMetadata<T>?> GetEntityAsync<T>(
        EntityInstanceId id,
        bool includeState = true,
        CancellationToken cancellation = default)
    {
        var entity = await entityBackendQueries.GetEntityAsync(
             id.ToCore(), includeState, false, cancellation);

        return Convert<T>(entity);
    }

    public override Task SignalEntityAsync(
        EntityInstanceId id,
        string operationName,
        object? input = null,
        SignalEntityOptions? options = null,
        CancellationToken cancellation = default)
    {
        var scheduledTime = options?.SignalTime;
        var serializedInput = dataConverter.Serialize(input);

        var eventToSend = ClientEntityHelpers.EmitOperationSignal(new OrchestrationInstance() { InstanceId = id.ToString() },
            Guid.NewGuid(),
            operationName,
            serializedInput,
            EntityMessageEvent.GetCappedScheduledTime(
                DateTime.UtcNow,
                TimeSpan.FromDays(3),
                scheduledTime?.UtcDateTime));

        return serviceClient.SendTaskOrchestrationMessageAsync(eventToSend.AsTaskMessage());
    }

    private AsyncPageable<TMetadata> GetAllEntitiesAsync<TMetadata>(
        Func<EntityBackendQueries.EntityMetadata, TMetadata> select,
        EntityQuery? filter)
        where TMetadata : notnull
    {
        return Pageable.Create(async (continuation, size, cancellation) =>
        {
            continuation ??= filter?.ContinuationToken;
            size ??= filter?.PageSize;
            EntityBackendQueries.EntityQueryResult result = await entityBackendQueries.QueryEntitiesAsync(
                new EntityBackendQueries.EntityQuery()
                {
                    InstanceIdStartsWith = filter?.InstanceIdStartsWith ?? string.Empty,
                    LastModifiedFrom = filter?.LastModifiedFrom?.UtcDateTime,
                    LastModifiedTo = filter?.LastModifiedTo?.UtcDateTime,
                    IncludeTransient = filter?.IncludeTransient ?? false,
                    IncludeState = filter?.IncludeState ?? true,
                    ContinuationToken = continuation,
                    PageSize = size,
                },
                cancellation);

            return new Page<TMetadata>([.. result.Results.Select(select)], result.ContinuationToken);
        });
    }

    [return: NotNullIfNotNull(nameof(metadata))]
    private EntityMetadata<T>? Convert<T>(EntityBackendQueries.EntityMetadata? metadata)
    {
        if (metadata is null)
        {
            return null;
        }

        return new EntityMetadata<T>(
            metadata.Value.EntityId.ToExtended(),
            dataConverter.Deserialize<T>(metadata.Value.SerializedState))
        {
            LastModifiedTime = metadata.Value.LastModifiedTime,
            BacklogQueueSize = metadata.Value.BacklogQueueSize,
            LockedBy = metadata.Value.LockedBy,
        };
    }

    [return: NotNullIfNotNull(nameof(metadata))]
    private EntityMetadata? Convert(EntityBackendQueries.EntityMetadata? metadata)
    {
        if (metadata is null)
        {
            return null;
        }

        var data = metadata.Value.SerializedState is null
            ? null
            : new SerializedData(metadata.Value.SerializedState, dataConverter);

        return new EntityMetadata(
            metadata.Value.EntityId.ToExtended(),
            data)
        {
            LastModifiedTime = metadata.Value.LastModifiedTime,
            BacklogQueueSize = metadata.Value.BacklogQueueSize,
            LockedBy = metadata.Value.LockedBy,
        };
    }

}

﻿using DurableTask.AspNetCore.Extensions;
using DurableTask.Core;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Options;


namespace DurableTask.AspNetCore;

// based on durabletask-dotnet/src/Client/OrchestrationServiceClientShim/ShimDurableTaskClient.cs
internal sealed class SelfHostedDurableTaskClient : DurableTaskClient
{
    private readonly TaskHubClient client;
    private readonly DurableTaskClientOptions options;

    public SelfHostedDurableTaskClient(string name, TaskHubClient client, IOptions<DurableTaskClientOptions> options) : base(name)
    {
        this.client = client;
        this.options = options.Value;
    }

    public override AsyncPageable<OrchestrationMetadata> GetAllInstancesAsync(OrchestrationQuery? filter = null)
    {
        if (client.ServiceClient is not Core.Query.IOrchestrationServiceQueryClient queryClient)
        {
            throw new NotSupportedException($"Provided IOrchestrationServiceClient does not implement {typeof(Core.Query.IOrchestrationServiceQueryClient)}.");
        }

        return Pageable.Create(async (continuation, pageSize, cancellation) =>
        {
            var coreQuery = new Core.Query.OrchestrationQuery()
            {
                RuntimeStatus = filter?.Statuses?.Select(x => x.ToCoreStatus()).ToArray(),
                CreatedTimeFrom = filter?.CreatedFrom?.UtcDateTime,
                CreatedTimeTo = filter?.CreatedTo?.UtcDateTime,
                TaskHubNames = filter?.TaskHubNames?.ToList(),
                PageSize = pageSize ?? filter?.PageSize ?? OrchestrationQuery.DefaultPageSize,
                ContinuationToken = continuation ?? filter?.ContinuationToken,
                InstanceIdPrefix = filter?.InstanceIdPrefix,
                FetchInputsAndOutputs = filter?.FetchInputsAndOutputs ?? false,
            };

            var result = await queryClient.GetOrchestrationWithQueryAsync(coreQuery, cancellation);

            var metadata = result.OrchestrationState
                .Select(x => CreateMetadata(x, coreQuery.FetchInputsAndOutputs))
                .ToList();

            return new Page<OrchestrationMetadata>(metadata, result.ContinuationToken);
        });
    }

    public override async Task<OrchestrationMetadata?> GetInstancesAsync(string instanceId, bool getInputsAndOutputs = false, CancellationToken cancellation = default)
    {
        cancellation.ThrowIfCancellationRequested();
        var state = await client.GetOrchestrationStateAsync(instanceId);
        if (state == null)
        {
            return null;
        }

        return CreateMetadata(state, getInputsAndOutputs);
    }

    public override Task RaiseEventAsync(string instanceId, string eventName, object? eventPayload = null, CancellationToken cancellation = default)
    {
        cancellation.ThrowIfCancellationRequested();
        // data converter can handle null payloads
        return client.RaiseEventAsync(CreateOrchestrationInstance(instanceId), eventName, eventPayload!);
    }

    public override Task ResumeInstanceAsync(string instanceId, string? reason = null, CancellationToken cancellation = default)
    {
        cancellation.ThrowIfCancellationRequested();
        return client.ResumeInstanceAsync(CreateOrchestrationInstance(instanceId), reason);
    }

    public override async Task<string> ScheduleNewOrchestrationInstanceAsync(TaskName orchestratorName, object? input = null, StartOrchestrationOptions? options = null, CancellationToken cancellation = default)
    {
        cancellation.ThrowIfCancellationRequested();
        var instance = await client.CreateOrchestrationInstanceAsync(
            orchestratorName.Name,
            orchestratorName.Version,
            options?.InstanceId ?? Guid.NewGuid().ToString("N"),
            input,
            null,
            null,
            options?.StartAt?.UtcDateTime ?? DateTime.UtcNow
            );

        return instance.InstanceId;
    }

    public override Task SuspendInstanceAsync(string instanceId, string? reason = null, CancellationToken cancellation = default)
    {
        cancellation.ThrowIfCancellationRequested();
        return client.SuspendInstanceAsync(CreateOrchestrationInstance(instanceId), reason);
    }

    public override async Task<OrchestrationMetadata> WaitForInstanceCompletionAsync(string instanceId, bool getInputsAndOutputs = false, CancellationToken cancellation = default)
    {
        var state = await client.WaitForOrchestrationAsync(CreateOrchestrationInstance(instanceId), Timeout.InfiniteTimeSpan, cancellation);
        return CreateMetadata(state, getInputsAndOutputs);
    }

    public override async Task<OrchestrationMetadata> WaitForInstanceStartAsync(string instanceId, bool getInputsAndOutputs = false, CancellationToken cancellation = default)
    {
        while (true)
        {
            var metadata = await GetInstancesAsync(instanceId, getInputsAndOutputs, cancellation)
                ?? throw new InvalidOperationException($"Orchestration with instanceId '{instanceId}' does not exist");

            if (metadata.RuntimeStatus != OrchestrationRuntimeStatus.Pending)
            {
                return metadata;
            }

            await Task.Delay(TimeSpan.FromSeconds(1), cancellation);
        }
    }

    public override Task TerminateInstanceAsync(string instanceId, TerminateInstanceOptions? options = null, CancellationToken cancellation = default)
    {
        cancellation.ThrowIfCancellationRequested();
        var reason = this.options.DataConverter.Serialize(options?.Output);

        // terminate can handle null payloads
        return client.TerminateInstanceAsync(CreateOrchestrationInstance(instanceId), reason!);
    }

    public override ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }

    private static OrchestrationInstance CreateOrchestrationInstance(string instanceId)
        => new() { InstanceId = instanceId };

    private OrchestrationMetadata CreateMetadata(OrchestrationState state, bool includeInputsAndOutputs)
    {
        return new(state.Name, state.OrchestrationInstance.InstanceId)
        {
            CreatedAt = state.CreatedTime,
            LastUpdatedAt = state.LastUpdatedTime,
            RuntimeStatus = state.OrchestrationStatus.ToRuntimeStatus(),
            SerializedInput = state.Input,
            SerializedOutput = state.Output,
            SerializedCustomStatus = state.Status,
            FailureDetails = state.FailureDetails?.ToTaskFailureDetails(),
            DataConverter = includeInputsAndOutputs ? this.options.DataConverter : null,
        };
    }
}
using DurableTask.Core;
using Microsoft.DurableTask.Client;

namespace DurableTask.AspNetCore.Extensions;

internal static class OrchestrationStatusExtensions
{
    public static OrchestrationStatus ToCoreStatus(this OrchestrationRuntimeStatus status)
    {
#pragma warning disable CS0618 // Type or member is obsolete
        return status switch
        {
            OrchestrationRuntimeStatus.Running => OrchestrationStatus.Running,
            OrchestrationRuntimeStatus.Completed => OrchestrationStatus.Completed,
            OrchestrationRuntimeStatus.ContinuedAsNew => OrchestrationStatus.ContinuedAsNew,
            OrchestrationRuntimeStatus.Failed => OrchestrationStatus.Failed,
            OrchestrationRuntimeStatus.Canceled => OrchestrationStatus.Canceled,
            OrchestrationRuntimeStatus.Terminated => OrchestrationStatus.Terminated,
            OrchestrationRuntimeStatus.Pending => OrchestrationStatus.Pending,
            OrchestrationRuntimeStatus.Suspended => OrchestrationStatus.Suspended,
            _ => (OrchestrationStatus)status,
        };
#pragma warning restore CS0618 // Type or member is obsolete
    }

    public static OrchestrationRuntimeStatus ToRuntimeStatus(this OrchestrationStatus status)
    {
#pragma warning disable CS0618 // Type or member is obsolete
        return status switch
        {
            OrchestrationStatus.Running => OrchestrationRuntimeStatus.Running,
            OrchestrationStatus.Completed => OrchestrationRuntimeStatus.Completed,
            OrchestrationStatus.ContinuedAsNew => OrchestrationRuntimeStatus.ContinuedAsNew,
            OrchestrationStatus.Failed => OrchestrationRuntimeStatus.Failed,
            OrchestrationStatus.Canceled => OrchestrationRuntimeStatus.Canceled,
            OrchestrationStatus.Terminated => OrchestrationRuntimeStatus.Terminated,
            OrchestrationStatus.Pending => OrchestrationRuntimeStatus.Pending,
            OrchestrationStatus.Suspended => OrchestrationRuntimeStatus.Suspended,
            _ => (OrchestrationRuntimeStatus)status,
        };
#pragma warning restore CS0618 // Type or member is obsolete
    }
}

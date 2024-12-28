using Microsoft.DurableTask.Client;
using Microsoft.DurableTask.Worker;

#pragma warning disable IDE0130
namespace DurableTask.AspNetCore;

public static class DurableTaskWorkerBuilderExtensions
{
    public static IDurableTaskWorkerBuilder UseSelfHosted(this IDurableTaskWorkerBuilder builder)
        => builder.UseBuildTarget<SelfHostedDurableTaskWorker>();
}

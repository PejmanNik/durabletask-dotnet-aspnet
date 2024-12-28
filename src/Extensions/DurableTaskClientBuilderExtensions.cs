using Microsoft.DurableTask.Client;
using Microsoft.DurableTask.Worker;

#pragma warning disable IDE0130
namespace DurableTask.AspNetCore;

public static class DurableTaskClientBuilderExtensions
{
    public static IDurableTaskClientBuilder UseSelfHosted(this IDurableTaskClientBuilder builder)
        => builder.UseBuildTarget<SelfHostedDurableTaskClient>();
}

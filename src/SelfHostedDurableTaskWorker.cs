using DurableTask.Core;
using Microsoft.DurableTask.Worker;
using Microsoft.DurableTask.Worker.Hosting;
using Microsoft.DurableTask.Worker.Shims;

namespace DurableTask.AspNetCore;

internal sealed class SelfHostedDurableTaskWorker : DurableTaskWorker
{
    private readonly TaskHubWorker worker;
    private readonly IWorkerDispatcherMiddleware middleware;

    public SelfHostedDurableTaskWorker(
        string? name,
        IDurableTaskFactory factory,
        TaskHubWorker taskHubWorker,
        IWorkerDispatcherMiddleware middleware) 
        : base(name, factory)
    {
        worker = taskHubWorker;
        this.middleware = middleware;
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        worker.AddOrchestrationDispatcherMiddleware((context, next) => middleware.OrchestrationMiddleware(Factory, context, next));
        worker.AddActivityDispatcherMiddleware((context, next) => middleware.ActivityMiddleware(Factory, context, next));
        await worker.StartAsync().WaitAsync(cancellationToken);
    }
}

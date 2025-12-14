using DurableTask.Core;
using Microsoft.DurableTask.Worker;
using Microsoft.DurableTask.Worker.Hosting;

namespace DurableTask.AspNetCore;

internal sealed class SelfHostedDurableTaskWorker : DurableTaskWorker
{
    private readonly IDurableTaskFactory2 factory;
    private readonly IWorkerDispatcherMiddleware middleware;
    private readonly TaskHubWorker worker;

    public SelfHostedDurableTaskWorker(
        string? name,
        IDurableTaskFactory2 factory,
        TaskHubWorker taskHubWorker,
        IWorkerDispatcherMiddleware middleware)
        : base(name, factory)
    {
        worker = taskHubWorker;
        this.middleware = middleware;
        this.factory = factory;
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        worker.AddOrchestrationDispatcherMiddleware((context, next) =>
            middleware.OrchestrationMiddleware(factory, context, next));
        worker.AddActivityDispatcherMiddleware((context, next) =>
            middleware.ActivityMiddleware(factory, context, next));
        worker.AddEntityDispatcherMiddleware((context, next) =>
            middleware.EntityMiddleware(factory, context, next));

        await worker.StartAsync().WaitAsync(cancellationToken);
    }
}
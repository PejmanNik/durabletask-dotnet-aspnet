using DurableTask.AspNetCore.Utilize;
using DurableTask.Core;
using DurableTask.Core.Entities;
using DurableTask.Core.Entities.OperationFormat;
using DurableTask.Core.History;
using DurableTask.Core.Middleware;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Worker;
using Microsoft.DurableTask.Worker.Shims;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DurableTask.AspNetCore;

internal interface IWorkerDispatcherMiddleware
{
    Task ActivityMiddleware(IDurableTaskFactory factory, DispatchMiddlewareContext context, Func<Task> next);
    Task OrchestrationMiddleware(IDurableTaskFactory factory, DispatchMiddlewareContext context, Func<Task> next);
}

internal sealed class WorkerDispatcherMiddleware : IWorkerDispatcherMiddleware
{
    private readonly DurableTaskShimFactory taskShimFactory;
    private readonly IServiceProvider serviceProvider;

    public WorkerDispatcherMiddleware(
        IServiceProvider serviceProvider,
        ILoggerFactory loggerFactory,
        IOptions<DurableTaskWorkerOptions> options)
    {
        this.taskShimFactory = new DurableTaskShimFactory(options.Value, loggerFactory); ;
        this.serviceProvider = serviceProvider;
    }

    public async Task ActivityMiddleware(
        IDurableTaskFactory factory,
        DispatchMiddlewareContext context,
        Func<Task> next)
    {
        var instance = context.GetProperty<OrchestrationInstance>();
        var scheduledEvent = context.GetProperty<TaskScheduledEvent>();
        var coreTaskActivity = context.GetProperty<TaskActivity>();

        if (scheduledEvent.Name is null)
        {
            throw new InvalidOperationException($"{nameof(TaskScheduledEvent)}.Name is null.");
        }
        if (coreTaskActivity is not ShimTaskActivity shimTaskActivity)
        {
            throw new InvalidOperationException($"{nameof(TaskActivity)} is not {nameof(ShimTaskActivity)}.");
        }

        await using var scope = serviceProvider.CreateAsyncScope();
        if (!factory.TryCreateActivity(scheduledEvent.Name, scope.ServiceProvider, out var activity))
        {
            throw new InvalidOperationException($"Activity {scheduledEvent.Name} not found.");
        }

        var taskActivity = taskShimFactory.CreateActivity(scheduledEvent.Name, activity);
        var taskContext = new TaskContext(instance);

        /// The durable task library, does not provide a way to handle
        /// task activity execution directly in middleware (unlike orchestrations)
        /// so we need to inject the actual execution function into the shim
        shimTaskActivity.SetExecutionFunction(() => taskActivity.RunAsync(taskContext, scheduledEvent.Input));

        await next();
    }

    public async Task OrchestrationMiddleware(
        IDurableTaskFactory factory,
        DispatchMiddlewareContext context,
        Func<Task> next)
    {
        var runtimeState = context.GetProperty<OrchestrationRuntimeState>();
        await using var scope = serviceProvider.CreateAsyncScope();

        if (!factory.TryCreateOrchestrator(runtimeState.Name, scope.ServiceProvider, out var orchestrator))
        {
            throw new InvalidOperationException($"Orchestration {runtimeState.Name} not found.");
        }

        var parent = runtimeState.ParentInstance switch
        {
            ParentInstance p => new ParentOrchestrationInstance(new(p.Name), p.OrchestrationInstance.InstanceId),
            _ => null,
        };

        var taskOrchestration = taskShimFactory.CreateOrchestration(runtimeState.Name, orchestrator, parent);
        var result = new TaskOrchestrationExecutor(
            runtimeState,
            taskOrchestration,
            BehaviorOnContinueAsNew.Carryover,
            context.GetProperty<TaskOrchestrationEntityParameters>(),
            ErrorPropagationMode.UseFailureDetails)
            .Execute();

        context.SetProperty(result);
        await next();
    }
}

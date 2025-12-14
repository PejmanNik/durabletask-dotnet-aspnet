using DurableTask.AspNetCore.Utilize;
using DurableTask.Core;
using DurableTask.Core.Entities.OperationFormat;
using DurableTask.Core.History;
using DurableTask.Core.Middleware;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Entities;
using Microsoft.DurableTask.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace DurableTask.AspNetCore.Tests;

public class WorkerDispatcherMiddlewareTests
{
    private readonly DispatchMiddlewareContext context;
    private readonly WorkerDispatcherMiddleware middleware;
    private readonly Mock<Func<Task>> next;
    private readonly Mock<IDurableTaskFactory2> taskFactory;

    public WorkerDispatcherMiddlewareTests()
    {
        var serviceProvider = new Mock<IServiceProvider>();
        var workerOptions = new Mock<IOptions<DurableTaskWorkerOptions>>();
        taskFactory = new Mock<IDurableTaskFactory2>();
        context = new DispatchMiddlewareContext();
        next = new Mock<Func<Task>>();

        workerOptions
            .Setup(o => o.Value)
            .Returns(new DurableTaskWorkerOptions());

        var serviceScopeFactory = new Mock<IServiceScopeFactory>();
        serviceProvider
            .Setup(o => o.GetService(typeof(IServiceScopeFactory)))
            .Returns(serviceScopeFactory.Object);
        serviceScopeFactory
            .Setup(o => o.CreateScope())
            .Returns(Mock.Of<IServiceScope>());

        middleware = new WorkerDispatcherMiddleware(
            serviceProvider.Object,
            Mock.Of<ILoggerFactory>(),
            workerOptions.Object);
    }

    [Fact]
    public async Task ActivityMiddleware_WhenCoreTaskActivityIsNotShimTaskActivity_ShouldThrowException()
    {
        context.SetProperty(new TaskScheduledEvent(-1));
        context.SetProperty(Mock.Of<TaskActivity>());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            middleware.ActivityMiddleware(taskFactory.Object, context, next.Object));
    }

    [Fact]
    public async Task ActivityMiddleware_WhenActivityNotFound_ShouldThrowException()
    {
        context.SetProperty(new TaskScheduledEvent(-1, "name"));
        context.SetProperty<TaskActivity>(new ShimTaskActivity());

        taskFactory.Setup(f =>
                f.TryCreateActivity(It.IsAny<TaskName>(), It.IsAny<IServiceProvider>(),
                    out It.Ref<ITaskActivity?>.IsAny))
            .Returns(false);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            middleware.ActivityMiddleware(taskFactory.Object, context, next.Object));
    }

    [Fact]
    public async Task OrchestrationMiddleware_WhenOrchestratorNotFound_ShouldThrowException()
    {
        context.SetProperty(new OrchestrationRuntimeState());

        taskFactory.Setup(f => f.TryCreateOrchestrator(It.IsAny<TaskName>(), It.IsAny<IServiceProvider>(),
                out It.Ref<ITaskOrchestrator?>.IsAny))
            .Returns(false);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            middleware.OrchestrationMiddleware(taskFactory.Object, context, next.Object));
    }

    [Fact]
    public async Task OrchestrationMiddleware_WhenOrchestratorFound_ShouldSetResult()
    {
        context.SetProperty(new OrchestrationRuntimeState([
            new ExecutionStartedEvent(-1, "d")
            {
                Name = "name"
            }
        ]));

        var orchestrator = new Mock<ITaskOrchestrator>();
        taskFactory.Setup(f => f.TryCreateOrchestrator(It.IsAny<TaskName>(), It.IsAny<IServiceProvider>(),
                out It.Ref<ITaskOrchestrator?>.IsAny))
            .Callback(new TryCreateOrchestratorCallback((name, provider, out result) =>
            {
                result = orchestrator.Object;
            }))
            .Returns(true);

        await middleware.OrchestrationMiddleware(taskFactory.Object, context, next.Object);

        var result = context.GetProperty<OrchestratorExecutionResult>();
        Assert.NotNull(result);
    }

    [Fact]
    public async Task EntityMiddleware_WhenEntityNotFound_ShouldThrowException()
    {
        context.SetProperty(new EntityBatchRequest()
        {
            InstanceId = "@name@key"
        });
        context.SetProperty<TaskActivity>(new ShimTaskActivity());

        taskFactory.Setup(f =>
                f.TryCreateEntity(It.IsAny<TaskName>(), It.IsAny<IServiceProvider>(),
                    out It.Ref<ITaskEntity?>.IsAny))
            .Returns(false);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            middleware.EntityMiddleware(taskFactory.Object, context, next.Object));
        Assert.Contains("not found", exception.Message);
    }

    [Fact]
    public async Task EntityMiddleware_WhenEntityNameIsNotValid_ShouldThrowException()
    {
        context.SetProperty(new EntityBatchRequest()
        {
            InstanceId = "name"
        });
        context.SetProperty<TaskActivity>(new ShimTaskActivity());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            middleware.EntityMiddleware(taskFactory.Object, context, next.Object));
        Assert.Contains("name", exception.Message);
    }
    
    [Fact]
    public async Task EntityMiddleware_WhenOEntityFound_ShouldSetResult()
    {
        context.SetProperty(new EntityBatchRequest()
        {
            InstanceId = "@name@key",
            Operations = [
                new OperationRequest()
                {
                    Id= Guid.NewGuid(),
                    Input = "input",
                    Operation = "op"
                }
            ]
        });
        context.SetProperty<TaskActivity>(new ShimTaskActivity());

        var entity = new Mock<ITaskEntity>();
        taskFactory.Setup(f =>
                f.TryCreateEntity(It.IsAny<TaskName>(), It.IsAny<IServiceProvider>(),
                    out It.Ref<ITaskEntity?>.IsAny))
            .Callback(new TryCreateOrchestratorCallback((name, provider, out result) =>
            {
                result = entity.Object;
            }))
            .Returns(true);
        
        await middleware.EntityMiddleware(taskFactory.Object, context, next.Object);
        
        var result = context.GetProperty<EntityBatchResult>();
        Assert.NotNull(result);
    }

    private delegate void TryCreateOrchestratorCallback(TaskName name, IServiceProvider provider, out object result);
}
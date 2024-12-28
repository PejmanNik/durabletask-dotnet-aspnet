using DurableTask.AspNetCore.Utilize;
using DurableTask.Core;
using DurableTask.Core.History;
using DurableTask.Core.Middleware;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace DurableTask.AspNetCore.Tests;

public class WorkerDispatcherMiddlewareTests
{
    private readonly Mock<IServiceProvider> serviceProvider;
    private readonly Mock<IOptions<DurableTaskWorkerOptions>> options;
    private readonly Mock<IDurableTaskFactory> taskFactory;
    private readonly DispatchMiddlewareContext context;
    private readonly Mock<Func<Task>> next;
    private readonly WorkerDispatcherMiddleware middleware;

    public WorkerDispatcherMiddlewareTests()
    {
        serviceProvider = new Mock<IServiceProvider>();
        options = new Mock<IOptions<DurableTaskWorkerOptions>>();
        taskFactory = new Mock<IDurableTaskFactory>();
        context = new();
        next = new Mock<Func<Task>>();

        options
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
            options.Object);
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

        taskFactory.Setup(f => f.TryCreateActivity(It.IsAny<TaskName>(), It.IsAny<IServiceProvider>(), out It.Ref<ITaskActivity?>.IsAny))
            .Returns(false);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            middleware.ActivityMiddleware(taskFactory.Object, context, next.Object));
    }

    [Fact]
    public async Task OrchestrationMiddleware_WhenOrchestratorNotFound_ShouldThrowException()
    {
        context.SetProperty(new OrchestrationRuntimeState());

        taskFactory.Setup(f => f.TryCreateOrchestrator(It.IsAny<TaskName>(), It.IsAny<IServiceProvider>(), out It.Ref<ITaskOrchestrator?>.IsAny))
            .Returns(false);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            middleware.OrchestrationMiddleware(taskFactory.Object, context, next.Object));
    }

    [Fact]
    public async Task OrchestrationMiddleware_ShouldSetResult_WhenOrchestratorFound()
    {
        context.SetProperty(new OrchestrationRuntimeState([
            new ExecutionStartedEvent(-1, "d")
            {
            Name = "name"
            }
        ]));

        var orchestrator = new Mock<ITaskOrchestrator>();
        taskFactory.Setup(f => f.TryCreateOrchestrator(It.IsAny<TaskName>(), It.IsAny<IServiceProvider>(), out It.Ref<ITaskOrchestrator?>.IsAny))
            .Callback(new TryCreateOrchestratorCallback((TaskName name, IServiceProvider provider, out object result) =>
            {
                result = orchestrator.Object;
            }))
            .Returns(true);

        await middleware.OrchestrationMiddleware(taskFactory.Object, context, next.Object);

        var result = context.GetProperty<OrchestratorExecutionResult>();
        Assert.NotNull(result);
    }

    private delegate void TryCreateOrchestratorCallback(TaskName name, IServiceProvider provider, out object result);
}
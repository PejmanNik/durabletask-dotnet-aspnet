using DurableTask.Core;

namespace DurableTask.AspNetCore.Utilize;

sealed class ShimActivityObjectManager : INameVersionObjectManager<TaskActivity>
{
    public void Add(ObjectCreator<TaskActivity> creator)
    {
        throw new NotImplementedException();
    }

    public TaskActivity? GetObject(string name, string? version)
    {
        return new ShimTaskActivity();
    }
}

/// <summary>
/// The durable task library, unlike orchestration, does not provide a way 
/// to handle task activity execution directly in middleware.
/// To address this, we need to create a shim activity that
/// the durable task worker can run.
/// This shim will enable us to handle the execution within the middleware.
/// </summary>
internal class ShimTaskActivity : TaskActivity
{
    private Func<Task<string>>? executionFunction;

    internal void SetExecutionFunction(Func<Task<string>> executionFunction)
    {
        this.executionFunction = executionFunction;
    }

    public override string Run(TaskContext context, string input)
    {
        throw new NotImplementedException();
    }

    public override Task<string> RunAsync(TaskContext context, string input)
    {
        return executionFunction?.Invoke() ?? Task.FromResult(string.Empty);
    }
}

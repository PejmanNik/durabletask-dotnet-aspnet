using DurableTask.Core;

namespace DurableTask.AspNetCore.Utilize;

sealed class ShimOrchestrationObjectManager : INameVersionObjectManager<TaskOrchestration>
{
    public void Add(ObjectCreator<TaskOrchestration> creator)
    {
        throw new NotImplementedException();
    }

    public TaskOrchestration? GetObject(string name, string? version)
    {
        return null;
    }
}

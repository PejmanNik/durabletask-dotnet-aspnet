using Microsoft.DurableTask.Entities;

namespace DurableTask.AspNetCore.Extensions;

internal static class EntityInstanceIdExtensions
{
    public static Core.Entities.EntityId ToCore(this EntityInstanceId id)
    {
        return new Core.Entities.EntityId(id.Name, id.Key);
    }

    public static EntityInstanceId ToExtended(this Core.Entities.EntityId id)
    {
        return new EntityInstanceId(id.Name, id.Key);
    }
}

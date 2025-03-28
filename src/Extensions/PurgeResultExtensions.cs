namespace DurableTask.AspNetCore.Extensions;

internal static class PurgeResultExtensions
{
    public static Microsoft.DurableTask.Client.PurgeResult ToExpanded(this DurableTask.Core.PurgeResult result)
    {
        return new Microsoft.DurableTask.Client.PurgeResult(
            result.DeletedInstanceCount);
    }
}

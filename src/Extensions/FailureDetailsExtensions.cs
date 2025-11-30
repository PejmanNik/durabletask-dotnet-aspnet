using DurableTask.Core;
using Microsoft.DurableTask;

namespace DurableTask.AspNetCore.Extensions;

internal static class FailureDetailsExtensions
{
    public static TaskFailureDetails? ToTaskFailureDetails(this FailureDetails? failureDetails)
    {
        if (failureDetails is null)
        {
            return null;
        }

        return new TaskFailureDetails(
            failureDetails.ErrorType,
            failureDetails.ErrorMessage,
            failureDetails.StackTrace,
            ToTaskFailureDetails(failureDetails.InnerFailure),
            failureDetails.Properties);
    }
}

using DurableTask.AspNetCore.Utilize;
using DurableTask.Core;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

#pragma warning disable IDE0130
namespace DurableTask.AspNetCore;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSelfHostedDurableTaskHub<TIOrchestrationService>(
       this IServiceCollection services,
       TIOrchestrationService orchestrationService
       ) where TIOrchestrationService : IOrchestrationService, IOrchestrationServiceClient
    {
        return services.AddSelfHostedDurableTaskHub(orchestrationService, orchestrationService);
    }

    public static IServiceCollection AddSelfHostedDurableTaskHub(
        this IServiceCollection services,
        IOrchestrationService orchestrationService,
        IOrchestrationServiceClient orchestrationServiceClient)
    {
        ArgumentNullException.ThrowIfNull(services, nameof(services));
        ArgumentNullException.ThrowIfNull(orchestrationService, nameof(orchestrationService));
        ArgumentNullException.ThrowIfNull(orchestrationServiceClient, nameof(orchestrationServiceClient));

        return services
            .AddSingleton<IWorkerDispatcherMiddleware, WorkerDispatcherMiddleware>()
            .AddSingleton((sp) => new TaskHubClient(
                orchestrationServiceClient,
                dataConverter: BuildDataConverter(sp),
                loggerFactory: sp.GetService<ILoggerFactory>())
            )
            .AddSingleton((sp) => new TaskHubWorker(
                orchestrationService,
                orchestrationObjectManager: new ShimOrchestrationObjectManager(),
                activityObjectManager: new ShimActivityObjectManager(),
                loggerFactory: sp.GetService<ILoggerFactory>())
            );
    }

    private static CoreDataConverterShim BuildDataConverter(IServiceProvider sp)
    {
        var options = sp.GetService<IOptions<DurableTaskClientOptions>>();
        if (options?.Value.DataConverter is null)
        {
            return new CoreDataConverterShim(Microsoft.DurableTask.Converters.JsonDataConverter.Default);
        }
        return new CoreDataConverterShim(options.Value.DataConverter);
    }
}

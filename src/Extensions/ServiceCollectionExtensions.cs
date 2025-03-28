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
        return services.AddSelfHostedDurableTaskHub((_) => orchestrationService, (_) => orchestrationService);
    }

    public static IServiceCollection AddSelfHostedDurableTaskHub<TIOrchestrationService>(
       this IServiceCollection services,
       Func<IServiceProvider, TIOrchestrationService> orchestrationServiceFactory)
       where TIOrchestrationService : IOrchestrationService, IOrchestrationServiceClient
    {
        return services.AddSelfHostedDurableTaskHub(orchestrationServiceFactory, orchestrationServiceFactory);
    }

    public static IServiceCollection AddSelfHostedDurableTaskHub<TIOrchestrationService, TIOrchestrationServiceClient>(
        this IServiceCollection services,
        Func<IServiceProvider, TIOrchestrationService> orchestrationServiceFactory,
        Func<IServiceProvider, TIOrchestrationServiceClient> orchestrationServiceClientFactory)
        where TIOrchestrationService : IOrchestrationService
        where TIOrchestrationServiceClient : IOrchestrationServiceClient
    {
        ArgumentNullException.ThrowIfNull(services, nameof(services));
        ArgumentNullException.ThrowIfNull(orchestrationServiceFactory, nameof(orchestrationServiceFactory));
        ArgumentNullException.ThrowIfNull(orchestrationServiceClientFactory, nameof(orchestrationServiceClientFactory));

        return services
            .AddSingleton<IWorkerDispatcherMiddleware, WorkerDispatcherMiddleware>()
            .AddSingleton((sp) => new TaskHubClient(
                orchestrationServiceClientFactory(sp),
                dataConverter: BuildDataConverter(sp),
                loggerFactory: sp.GetService<ILoggerFactory>())
            )
            .AddSingleton((sp) => new TaskHubWorker(
                orchestrationServiceFactory(sp),
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

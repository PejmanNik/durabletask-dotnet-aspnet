using DurableTask.AspNetCore.Utilize;
using DurableTask.Core;
using Microsoft.DurableTask.Client;
using Microsoft.DurableTask.Converters;
using Microsoft.DurableTask.Worker;
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
        return services.AddSelfHostedDurableTaskHub(_ => orchestrationService, _ => orchestrationService);
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
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(orchestrationServiceFactory);
        ArgumentNullException.ThrowIfNull(orchestrationServiceClientFactory);

        return services
            .AddSingleton<IWorkerDispatcherMiddleware, WorkerDispatcherMiddleware>()
            .AddSingleton(sp => new TaskHubClient(
                orchestrationServiceClientFactory(sp),
                BuildDataConverter(sp),
                sp.GetService<ILoggerFactory>())
            )
            .AddSingleton(sp => new TaskHubWorker(
                orchestrationServiceFactory(sp),
                new ShimOrchestrationObjectManager(),
                new ShimActivityObjectManager(),
                sp.GetService<ILoggerFactory>())
            )
            .AddSingleton(BuildDataConverter)
            .Configure<DurableTaskWorkerOptions>(o => { o.EnableEntitySupport = true; })
            .Configure<DurableTaskClientOptions>(o => { o.EnableEntitySupport = true; });
    }

    private static CoreDataConverterShim BuildDataConverter(IServiceProvider sp)
    {
        var options = sp.GetService<IOptions<DurableTaskClientOptions>>();
        if (options?.Value.DataConverter is null) return new CoreDataConverterShim(JsonDataConverter.Default);
        return new CoreDataConverterShim(options.Value.DataConverter);
    }
}
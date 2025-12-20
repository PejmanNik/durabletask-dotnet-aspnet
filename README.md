# Self-Hosted Durable Task Worker in Asp .Net Core

This project enables running DurableTask without a sidecar project, 
allowing the worker to be self-hosted. It provides a seamless way to integrate 
Durable Task functionality directly into your Asp .Net Core project.

## Usage

To use this project effectively, you should already understand the concepts of the Microsoft Durable Task Framework and how to work with it. Microsoft provides comprehensive documentation that covers these topics in detail.

Install the [![NuGet version (Sisu.DurableTask.AspNetCore)](https://img.shields.io/nuget/vpre/Sisu.DurableTask.AspNetCore)](https://www.nuget.org/packages/Sisu.DurableTask.AspNetCore/) package.

```
dotnet add package Sisu.DurableTask.AspNetCore
```

> [!NOTE]
> To respect the DurableTask domain and reserve it for official packages, 
I added a prefix to the project assembly. In Finnish culture, **sisu** represents determination, perseverance, and resilience, which felt like a fitting touch!

Then, register the host with your preferred [orchestration service](https://github.com/Azure/durabletask?tab=readme-ov-file#supported-persistance-stores)

``` csharp
using DurableTask.AspNetCore;
using DurableTask.AzureStorage;

var orchestrationServiceAndClient = new AzureStorageOrchestrationService(new()
{
    StorageAccountClientProvider = new StorageAccountClientProvider("...."),
});

builder.Services.AddSelfHostedDurableTaskHub(orchestrationServiceAndClient);
```

Finally, register the [durabletask-dotnet](https://github.com/microsoft/durabletask-dotnet) services and add `UseSelfHosted` to both the worker and client.

``` csharp
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.DurableTask.Worker;

// Add all the generated tasks
builder.Services.AddDurableTaskWorker(builder =>
{
    builder
        .AddTasks(r => r.AddAllGeneratedTasks());
        .UseSelfHosted();
});

builder.Services.AddDurableTaskClient(b => b.UseSelfHosted());

```

Now you can inject the `DurableTaskClient` into your classes to schedule or manage durable tasks. For more detailed examples, check out the samples folder.

### Durable Entities

To use durable entities, **Enable entity work item separation** by setting `UseSeparateQueueForEntityWorkItems = true` in your `OrchestrationService`.

```csharp

// Configure the orchestration service
var orchestrationServiceAndClient = new AzureStorageOrchestrationService(new()
{
    ...
    UseSeparateQueueForEntityWorkItems = true
});

// Register the worker and tasks
builder.Services.AddDurableTaskWorker(builder =>
{
    builder
        .AddTasks(r => r.AddAllGeneratedTasks())
        .UseSelfHosted();
});

[DurableTask]
public sealed class MyCounterEntity : TaskEntity<int>
{
    public void Add(int amount)
    {
        State += amount;
    }
}
```

For more detailed examples, check out the samples folder.

## Under the Hood

The [durabletask-dotnet](https://github.com/microsoft/durabletask-dotnet) project is built on top of 
the [Durable Task Framework](https://github.com/Azure/durabletask?tab=readme-ov-file#supported-persistance-stores). 
It provides an easy way to run durable tasks using Dependency Injection and `IHostedService` as a background service in your application. 
One of its standout features is a type-safe source generator for orchestrators and activities, making it a breeze to work with. However,
"It's specifically designed to connect to a "sidecar" process, such as the Azure Functions .NET Isolated host, a special purpose sidecar container, or potentially even Dapr.",
so it use gRPC to communicate with the sidecar. In this project the gRPC communication 
is replaced with a direct call to the `DurableTaskHub` service that runs in the same process.

## Acknowledgements

[durabletask-dotnet](https://github.com/microsoft/durabletask-dotnet) for providing the core Durable Task framework.
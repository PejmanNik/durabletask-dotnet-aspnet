using DurableTask.AspNetCore;
using DurableTask.AzureStorage;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.DurableTask.Worker;
using WebAPI;

var builder = WebApplication.CreateBuilder(args);

var durableTaskStorage = builder.Configuration.GetValue<string>("DurableTaskStorage")
    ?? throw new InvalidOperationException("Connection string for 'DurableTaskStorage' not found.");

var orchestrationServiceAndClient = new AzureStorageOrchestrationService(new()
{
    StorageAccountClientProvider = new StorageAccountClientProvider(durableTaskStorage),
    TaskHubName = "hub1",
    WorkerId = "worker1",
});

builder.Services.AddSelfHostedDurableTaskHub(orchestrationServiceAndClient);
builder.Services.AddDurableTaskWorker(builder =>
{
    builder
        .AddTasks(r => r.AddAllGeneratedTasks())
        .UseSelfHosted();
});

builder.Services.AddDurableTaskClient(b => b.UseSelfHosted());

var app = builder.Build();

app.UseHttpsRedirection();

app.MapBurgerApi();
app.Run();


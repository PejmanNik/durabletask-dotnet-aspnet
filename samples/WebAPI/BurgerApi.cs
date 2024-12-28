using Microsoft.AspNetCore.Mvc;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;

namespace WebAPI;

internal static class BurgerApi
{
    public static void MapBurgerApi(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/orders", async (GrillBurgerInput input, [FromServices] DurableTaskClient client) =>
        {
            var id = await client.ScheduleNewGrillBurgerOrchestratorInstanceAsync(input);
            return new
            {
                id
            };
        })
        .WithName("OrderBurger");

        endpoints.MapGet("/orders", async (string id, [FromServices] DurableTaskClient client) =>
        {
            return await client.GetInstanceAsync(id);
        })
        .WithName("BurgerStatus");
    }
}


public class GrillBurgerInput(string patty, string bun)
{
    public string Patty { get; } = patty;
    public string Bun { get; } = bun;
}

[DurableTask]
internal sealed class GrillBurgerOrchestrator : TaskOrchestrator<GrillBurgerInput, string>
{
    public override async Task<string> RunAsync(
        TaskOrchestrationContext context,
        GrillBurgerInput input)
    {
        var logger = context.CreateReplaySafeLogger(nameof(GrillBurgerOrchestrator));
        logger.LogInformation("Orchestrator started");

        // 1. Grill the Patty in parallel with Toasting the Bun
        var tasks = new List<Task<string>>
        {
            context.CallGrillPattyActivityAsync(input.Patty),
            context.CallToastBunActivityAsync(input.Bun)
        };

        var results = await Task.WhenAll(tasks);
  
        // 2. Assemble the Burger
        var burger = await context.CallAssembleBurgerActivityAsync(results);

        logger.LogInformation("Orchestrator completed");
        return $"Burger is ready: {burger}";
    }
}

[DurableTask]
internal sealed class GrillPattyActivity : TaskActivity<string, string>
{
    private readonly ILogger<GrillPattyActivity> _logger;

    public GrillPattyActivity(ILogger<GrillPattyActivity> logger)
    {
        _logger = logger;
    }

    public override async Task<string> RunAsync(
        TaskActivityContext context,
        string patty)
    {
        _logger.LogInformation("Grilling patty...");
        await Task.Delay(TimeSpan.FromSeconds(3)); // Simulate grilling time
        return $"{patty} (Grilled)";
    }
}

[DurableTask]
internal sealed class ToastBunActivity : TaskActivity<string, string>
{
    private readonly ILogger<ToastBunActivity> _logger;

    public ToastBunActivity(ILogger<ToastBunActivity> logger)
    {
        _logger = logger;
    }

    public override async Task<string> RunAsync(
        TaskActivityContext context,
        string bun)
    {
        _logger.LogInformation("Toasting bun...");
        await Task.Delay(TimeSpan.FromSeconds(1)); // Simulate toasting time
        return $"{bun} (Toasted)";
    }
}

[DurableTask]
internal sealed class AssembleBurgerActivity : TaskActivity<IEnumerable<string>, string>
{
    private readonly ILogger<AssembleBurgerActivity> _logger;

    public AssembleBurgerActivity(ILogger<AssembleBurgerActivity> logger)
    {
        _logger = logger;
    }

    public override async Task<string> RunAsync(
        TaskActivityContext context,
        IEnumerable<string> inputs)
    {
        _logger.LogInformation("Assembling burger...");
        await Task.Delay(TimeSpan.FromSeconds(1)); // Simulate assembly time
        return $"Burger with {string.Join(',', inputs)}";
    }
}
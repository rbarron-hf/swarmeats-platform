using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

// ═══════════════════════════════════════════════════════════════
// {ContextName} Function App — Entry Point
//
// Replace {ContextName} with: Orders | Restaurant | Delivery
// Replace repository registrations with context-specific ones.
// ═══════════════════════════════════════════════════════════════

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices(services =>
    {
        // ── MediatR: auto-register all handlers in this assembly ──
        services.AddMediatR(cfg =>
            cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));

        // ── Cosmos DB client (singleton — thread-safe, connection pooled) ──
        services.AddSingleton(sp =>
        {
            var connectionString = Environment.GetEnvironmentVariable("CosmosDBConnection")
                ?? throw new InvalidOperationException("CosmosDBConnection not configured.");

            return new CosmosClient(connectionString, new CosmosClientOptions
            {
                SerializerOptions = new CosmosSerializationOptions
                {
                    PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase
                },
                ConnectionMode = ConnectionMode.Direct
            });
        });

        // ── Repository registrations (scoped — one per function invocation) ──
        // TODO: Replace with context-specific repositories:
        //
        // Orders context:
        //   services.AddScoped<IOrderRepository>(sp => ...);
        //
        // Restaurant context:
        //   services.AddScoped<IMenuRepository>(sp => ...);
        //   services.AddScoped<IRestaurantOrderRepository>(sp => ...);
        //
        // Delivery context:
        //   services.AddScoped<IDeliveryRepository>(sp => ...);
        //   services.AddScoped<IDriverRepository>(sp => ...);

        // ── Application Insights (optional) ──
        services.AddApplicationInsightsTelemetryWorkerService();
    })
    .Build();

host.Run();

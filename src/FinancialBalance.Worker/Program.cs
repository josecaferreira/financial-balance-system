using FinancialBalance.Application.Common;
using FinancialBalance.Domain.Reporting;
using FinancialBalance.Worker.Consumers;
using FinancialBalance.Worker.Infrastructure.Cache;
using FinancialBalance.Worker.Infrastructure.Persistence;
using FinancialBalance.Worker.Infrastructure.Persistence.Repositories;
using FinancialBalance.Worker.Jobs;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using StackExchange.Redis;

var host = Host.CreateDefaultBuilder(args)
    .UseSerilog((ctx, cfg) => cfg
        .ReadFrom.Configuration(ctx.Configuration)
        .Enrich.FromLogContext()
        .WriteTo.Console())
    .ConfigureServices((ctx, services) =>
    {
        var config = ctx.Configuration;

        // EF Core — reporting schema
        services.AddDbContext<WorkerDbContext>(options =>
            options.UseNpgsql(
                config.GetConnectionString("Postgres"),
                npgsql => npgsql.MigrationsHistoryTable("__ef_reporting_migrations", "reporting")));

        // Repositories
        services.AddScoped<IDailySummaryRepository, DailySummaryRepository>();
        services.AddScoped<IMonthlySummaryRepository, MonthlySummaryRepository>();

        // Redis cache
        services.AddSingleton<IConnectionMultiplexer>(_ =>
            ConnectionMultiplexer.Connect(config.GetConnectionString("Redis")!));
        services.AddScoped<IReportCache, RedisReportCache>();

        // MassTransit + RabbitMQ
        services.AddMassTransit(x =>
        {
            x.AddConsumer<TransactionCreatedConsumer, TransactionCreatedConsumerDefinition>();
            x.AddConsumer<TransactionCancelledConsumer, TransactionCancelledConsumerDefinition>();

            x.UsingRabbitMq((ctx, cfg) =>
            {
                cfg.Host(config["RabbitMQ:Host"], h =>
                {
                    h.Username(config["RabbitMQ:Username"]!);
                    h.Password(config["RabbitMQ:Password"]!);
                });

                cfg.ConfigureEndpoints(ctx);
            });
        });

        // Background jobs
        services.AddHostedService<MonthlyRollupJob>();
        services.AddHostedService<DailySummaryCleanupJob>();

        // OpenTelemetry
        services.AddOpenTelemetry()
            .ConfigureResource(r => r.AddService("financial-balance-reporting-worker"))
            .WithTracing(t => t
                .AddEntityFrameworkCoreInstrumentation()
                .AddSource("MassTransit"));
    })
    .Build();

// Apply pending EF Core migrations on startup
using (var scope = host.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<WorkerDbContext>();
    await db.Database.MigrateAsync();
}

await host.RunAsync();

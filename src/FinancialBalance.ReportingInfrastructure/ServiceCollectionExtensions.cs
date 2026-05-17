using FinancialBalance.Application.Common;
using FinancialBalance.Domain.Reporting;
using FinancialBalance.ReportingInfrastructure.Auth;
using FinancialBalance.ReportingInfrastructure.Cache;
using FinancialBalance.ReportingInfrastructure.Messaging;
using FinancialBalance.ReportingInfrastructure.Persistence;
using FinancialBalance.ReportingInfrastructure.Persistence.Repositories;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace FinancialBalance.ReportingInfrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddReportingInfrastructure(
        this IServiceCollection services, IConfiguration configuration)
    {
        // EF Core — reporting schema
        services.AddDbContext<ReportingDbContext>(options =>
            options.UseNpgsql(
                configuration.GetConnectionString("Postgres"),
                npgsql => npgsql.MigrationsHistoryTable("__ef_reporting_migrations", "reporting")));

        // Repositories
        services.AddScoped<IDailySummaryRepository, DailySummaryRepository>();
        services.AddScoped<IMonthlySummaryRepository, MonthlySummaryRepository>();

        // Redis cache
        services.AddSingleton<IConnectionMultiplexer>(_ =>
            ConnectionMultiplexer.Connect(configuration.GetConnectionString("Redis")!));
        services.AddScoped<IReportCache, RedisReportCache>();

        // Auth
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, CurrentUser>();

        // MassTransit — consume events from Transaction API
        services.AddMassTransit(x =>
        {
            x.AddConsumer<TransactionCreatedConsumer>();
            x.AddConsumer<TransactionCancelledConsumer>();

            x.UsingRabbitMq((ctx, cfg) =>
            {
                cfg.Host(configuration["RabbitMQ:Host"], h =>
                {
                    h.Username(configuration["RabbitMQ:Username"]!);
                    h.Password(configuration["RabbitMQ:Password"]!);
                });

                cfg.ReceiveEndpoint("reporting-transaction-created", e =>
                {
                    e.ConfigureConsumer<TransactionCreatedConsumer>(ctx);
                    e.UseMessageRetry(r => r.Intervals(TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(30)));
                });

                cfg.ReceiveEndpoint("reporting-transaction-cancelled", e =>
                {
                    e.ConfigureConsumer<TransactionCancelledConsumer>(ctx);
                    e.UseMessageRetry(r => r.Intervals(TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(15)));
                });
            });
        });

        // Monthly rollup background job
        services.AddHostedService<MonthlyRollupJob>();

        return services;
    }
}

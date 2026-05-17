using FinancialBalance.Application.Common;
using FinancialBalance.Domain.Accounts;
using FinancialBalance.Infrastructure.Auth;
using FinancialBalance.Infrastructure.Persistence;
using FinancialBalance.Infrastructure.Persistence.Repositories;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FinancialBalance.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services, IConfiguration configuration)
    {
        // EF Core
        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(
                configuration.GetConnectionString("Postgres"),
                npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", "finance")));

        // Repositories
        services.AddScoped<IAccountRepository, AccountRepository>();

        // Auth
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, CurrentUser>();

        // MassTransit + RabbitMQ
        services.AddMassTransit(x =>
        {
            x.UsingRabbitMq((ctx, cfg) =>
            {
                cfg.Host(configuration["RabbitMQ:Host"], h =>
                {
                    h.Username(configuration["RabbitMQ:Username"]!);
                    h.Password(configuration["RabbitMQ:Password"]!);
                });
                cfg.ConfigureEndpoints(ctx);
            });
        });

        // Outbox processor
        services.AddHostedService<OutboxProcessor>();

        return services;
    }
}

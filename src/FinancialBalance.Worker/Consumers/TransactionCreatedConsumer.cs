using FinancialBalance.Application.Common;
using FinancialBalance.Domain.Accounts.Events;
using FinancialBalance.Domain.Reporting;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace FinancialBalance.Worker.Consumers;

public class TransactionCreatedConsumer : IConsumer<TransactionCreated>
{
    private readonly IDailySummaryRepository _repository;
    private readonly IReportCache _cache;
    private readonly ILogger<TransactionCreatedConsumer> _logger;

    public TransactionCreatedConsumer(
        IDailySummaryRepository repository,
        IReportCache cache,
        ILogger<TransactionCreatedConsumer> logger)
    {
        _repository = repository;
        _cache = cache;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<TransactionCreated> context)
    {
        var msg = context.Message;
        var date = DateOnly.FromDateTime(msg.TransactionDate);

        _logger.LogInformation(
            "TransactionCreated received: {TransactionId} account={AccountId} type={Type} amount={Amount} date={Date}",
            msg.TransactionId, msg.AccountId, msg.Type, msg.Amount, date);

        var summary = await _repository.GetAsync(msg.AccountId, date, context.CancellationToken)
                      ?? DailySummary.Create(msg.AccountId, date);

        summary.ApplyTransaction(msg.Type.ToString(), msg.Amount, "Other");
        await _repository.UpsertAsync(summary, context.CancellationToken);

        // Invalidate cached reports for this account/date
        await InvalidateCacheAsync(msg.AccountId, date, context.CancellationToken);

        _logger.LogInformation(
            "DailySummary updated for account {AccountId} on {Date}: incoming={Incoming} outgoing={Outgoing}",
            msg.AccountId, date, summary.TotalIncoming, summary.TotalOutgoing);
    }

    private async Task InvalidateCacheAsync(Guid accountId, DateOnly date, CancellationToken ct)
    {
        await _cache.RemoveAsync($"daily:{accountId}:{date:yyyy-MM-dd}", ct);
        await _cache.RemoveAsync($"monthly:{accountId}:{date.Year}:{date.Month:D2}", ct);
    }
}

public class TransactionCreatedConsumerDefinition : ConsumerDefinition<TransactionCreatedConsumer>
{
    public TransactionCreatedConsumerDefinition()
    {
        EndpointName = "reporting-transaction-created";
        ConcurrentMessageLimit = 10;
    }

    protected override void ConfigureConsumer(
        IReceiveEndpointConfigurator endpointConfigurator,
        IConsumerConfigurator<TransactionCreatedConsumer> consumerConfigurator,
        IRegistrationContext context)
    {
        endpointConfigurator.UseMessageRetry(r =>
            r.Intervals(
                TimeSpan.FromSeconds(5),
                TimeSpan.FromSeconds(15),
                TimeSpan.FromSeconds(30),
                TimeSpan.FromMinutes(1)));

        endpointConfigurator.UseInMemoryOutbox(context);
    }
}

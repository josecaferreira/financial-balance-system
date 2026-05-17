using FinancialBalance.Application.Common;
using FinancialBalance.Domain.Accounts.Events;
using FinancialBalance.Domain.Reporting;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace FinancialBalance.ReportingInfrastructure.Messaging;

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
            "Processing TransactionCreated {TransactionId} for account {AccountId} on {Date}",
            msg.TransactionId, msg.AccountId, date);

        var summary = await _repository.GetAsync(msg.AccountId, date, context.CancellationToken)
                      ?? DailySummary.Create(msg.AccountId, date);

        summary.ApplyTransaction(msg.Type.ToString(), msg.Amount, "Other");
        await _repository.UpsertAsync(summary, context.CancellationToken);

        await _cache.RemoveAsync($"daily:{msg.AccountId}:{date:yyyy-MM-dd}", context.CancellationToken);
        await _cache.RemoveAsync($"monthly:{msg.AccountId}:{date.Year}:{date.Month:D2}", context.CancellationToken);
    }
}

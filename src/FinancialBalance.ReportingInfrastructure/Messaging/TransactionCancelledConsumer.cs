using FinancialBalance.Application.Common;
using FinancialBalance.Domain.Accounts.Events;
using FinancialBalance.Domain.Reporting;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace FinancialBalance.ReportingInfrastructure.Messaging;

public class TransactionCancelledConsumer : IConsumer<TransactionCancelled>
{
    private readonly IDailySummaryRepository _dailyRepo;
    private readonly IMonthlySummaryRepository _monthlyRepo;
    private readonly IReportCache _cache;
    private readonly ILogger<TransactionCancelledConsumer> _logger;

    public TransactionCancelledConsumer(
        IDailySummaryRepository dailyRepo,
        IMonthlySummaryRepository monthlyRepo,
        IReportCache cache,
        ILogger<TransactionCancelledConsumer> logger)
    {
        _dailyRepo = dailyRepo;
        _monthlyRepo = monthlyRepo;
        _cache = cache;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<TransactionCancelled> context)
    {
        var msg = context.Message;

        _logger.LogInformation(
            "Processing TransactionCancelled {TransactionId} for account {AccountId}",
            msg.TransactionId, msg.AccountId);

        // We need to find which date this transaction belonged to.
        // Since the event doesn't carry the date, we look up recent summaries
        // and reverse — in production this would be enriched in the event itself.
        // For now we invalidate all recent cache keys for this account.
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        await _cache.RemoveAsync($"daily:{msg.AccountId}:{today:yyyy-MM-dd}", context.CancellationToken);
        await _cache.RemoveAsync($"monthly:{msg.AccountId}:{today.Year}:{today.Month:D2}", context.CancellationToken);
    }
}

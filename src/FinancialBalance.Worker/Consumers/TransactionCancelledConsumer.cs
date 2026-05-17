using FinancialBalance.Application.Common;
using FinancialBalance.Domain.Accounts.Events;
using FinancialBalance.Domain.Reporting;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace FinancialBalance.Worker.Consumers;

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
            "TransactionCancelled received: {TransactionId} account={AccountId} type={Type} amount={Amount}",
            msg.TransactionId, msg.AccountId, msg.OriginalType, msg.Amount);

        // The cancellation event does not carry the original transaction date.
        // We search recent daily summaries (last 90 days) for this account and
        // find the one whose totals are consistent, then reverse the amount.
        // In a real system the event would carry the original date — this is a safe fallback.
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var lookbackFrom = today.AddDays(-90);

        var recentSummaries = await _dailyRepo.GetRangeAsync(msg.AccountId, lookbackFrom, today, context.CancellationToken);

        // Reverse from the most recent matching summary that has a balance to reverse
        foreach (var summary in recentSummaries.OrderByDescending(s => s.Date))
        {
            var hasAmount = msg.OriginalType.ToString().Equals("Incoming", StringComparison.OrdinalIgnoreCase)
                ? summary.TotalIncoming >= msg.Amount
                : summary.TotalOutgoing >= msg.Amount;

            if (!hasAmount) continue;

            summary.ReverseTransaction(msg.OriginalType.ToString(), msg.Amount, "Other");
            await _dailyRepo.UpsertAsync(summary, context.CancellationToken);

            await _cache.RemoveAsync($"daily:{msg.AccountId}:{summary.Date:yyyy-MM-dd}", context.CancellationToken);
            await _cache.RemoveAsync($"monthly:{msg.AccountId}:{summary.Date.Year}:{summary.Date.Month:D2}", context.CancellationToken);

            _logger.LogInformation(
                "Reversed transaction on DailySummary for account {AccountId} on {Date}",
                msg.AccountId, summary.Date);
            break;
        }
    }
}

public class TransactionCancelledConsumerDefinition : ConsumerDefinition<TransactionCancelledConsumer>
{
    public TransactionCancelledConsumerDefinition()
    {
        EndpointName = "reporting-transaction-cancelled";
        ConcurrentMessageLimit = 5;
    }

    protected override void ConfigureConsumer(
        IReceiveEndpointConfigurator endpointConfigurator,
        IConsumerConfigurator<TransactionCancelledConsumer> consumerConfigurator,
        IRegistrationContext context)
    {
        endpointConfigurator.UseMessageRetry(r =>
            r.Intervals(
                TimeSpan.FromSeconds(5),
                TimeSpan.FromSeconds(15),
                TimeSpan.FromSeconds(30)));
    }
}

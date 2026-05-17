using FinancialBalance.Application.Common;
using FinancialBalance.Domain.Accounts;
using FinancialBalance.Domain.Accounts.Events;
using FinancialBalance.Domain.Reporting;
using FinancialBalance.Worker.Consumers;
using FluentAssertions;
using MassTransit;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace FinancialBalance.Worker.Tests.Consumers;

public class TransactionCancelledConsumerTests
{
    private readonly IDailySummaryRepository _dailyRepo = Substitute.For<IDailySummaryRepository>();
    private readonly IMonthlySummaryRepository _monthlyRepo = Substitute.For<IMonthlySummaryRepository>();
    private readonly IReportCache _cache = Substitute.For<IReportCache>();
    private readonly TransactionCancelledConsumer _consumer;

    private static readonly Guid AccountId = Guid.NewGuid();

    public TransactionCancelledConsumerTests()
        => _consumer = new TransactionCancelledConsumer(
            _dailyRepo, _monthlyRepo, _cache,
            NullLogger<TransactionCancelledConsumer>.Instance);

    private ConsumeContext<TransactionCancelled> BuildContext(TransactionCancelled msg)
    {
        var ctx = Substitute.For<ConsumeContext<TransactionCancelled>>();
        ctx.Message.Returns(msg);
        ctx.CancellationToken.Returns(CancellationToken.None);
        return ctx;
    }

    [Fact]
    public async Task Consume_FindsMatchingSummaryAndReverses()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var summary = DailySummary.Create(AccountId, today);
        summary.ApplyTransaction("Incoming", 1000m, "Other");

        _dailyRepo.GetRangeAsync(AccountId, Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(new List<DailySummary> { summary });

        var msg = new TransactionCancelled(Guid.NewGuid(), AccountId, 1000m, TransactionType.Incoming);

        await _consumer.Consume(BuildContext(msg));

        await _dailyRepo.Received(1).UpsertAsync(
            Arg.Is<DailySummary>(s => s.TotalIncoming == 0m),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Consume_InvalidatesCacheKeysAfterReversal()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var summary = DailySummary.Create(AccountId, today);
        summary.ApplyTransaction("Outgoing", 500m, "Other");

        _dailyRepo.GetRangeAsync(AccountId, Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(new List<DailySummary> { summary });

        var msg = new TransactionCancelled(Guid.NewGuid(), AccountId, 500m, TransactionType.Outgoing);

        await _consumer.Consume(BuildContext(msg));

        await _cache.Received(1).RemoveAsync(
            Arg.Is<string>(k => k.StartsWith($"daily:{AccountId}")),
            Arg.Any<CancellationToken>());
        await _cache.Received(1).RemoveAsync(
            Arg.Is<string>(k => k.StartsWith($"monthly:{AccountId}")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Consume_WhenNoMatchingSummary_DoesNotUpsert()
    {
        // Summary with only outgoing, but cancellation is for incoming
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var summary = DailySummary.Create(AccountId, today);
        summary.ApplyTransaction("Outgoing", 100m, "Other");

        _dailyRepo.GetRangeAsync(AccountId, Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(new List<DailySummary> { summary });

        // Cancelling 5000 incoming but summary only has 100 outgoing — no match
        var msg = new TransactionCancelled(Guid.NewGuid(), AccountId, 5000m, TransactionType.Incoming);

        await _consumer.Consume(BuildContext(msg));

        await _dailyRepo.DidNotReceive().UpsertAsync(Arg.Any<DailySummary>(), Arg.Any<CancellationToken>());
    }
}

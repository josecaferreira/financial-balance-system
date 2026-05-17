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

public class TransactionCreatedConsumerTests
{
    private readonly IDailySummaryRepository _dailyRepo = Substitute.For<IDailySummaryRepository>();
    private readonly IReportCache _cache = Substitute.For<IReportCache>();
    private readonly TransactionCreatedConsumer _consumer;

    private static readonly Guid AccountId = Guid.NewGuid();
    private static readonly DateTime TxDate = new(2024, 6, 15, 10, 0, 0, DateTimeKind.Utc);

    public TransactionCreatedConsumerTests()
        => _consumer = new TransactionCreatedConsumer(_dailyRepo, _cache, NullLogger<TransactionCreatedConsumer>.Instance);

    private ConsumeContext<TransactionCreated> BuildContext(TransactionCreated msg)
    {
        var ctx = Substitute.For<ConsumeContext<TransactionCreated>>();
        ctx.Message.Returns(msg);
        ctx.CancellationToken.Returns(CancellationToken.None);
        return ctx;
    }

    [Fact]
    public async Task Consume_WhenNoDailySummaryExists_CreatesAndUpserts()
    {
        _dailyRepo.GetAsync(AccountId, Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns((DailySummary?)null);

        var msg = new TransactionCreated(Guid.NewGuid(), AccountId, TransactionType.Incoming, 1000m, TxDate);

        await _consumer.Consume(BuildContext(msg));

        await _dailyRepo.Received(1).UpsertAsync(
            Arg.Is<DailySummary>(s => s.AccountId == AccountId && s.TotalIncoming == 1000m),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Consume_WhenDailySummaryExists_AppliesTransactionToExisting()
    {
        var existing = DailySummary.Create(AccountId, DateOnly.FromDateTime(TxDate));
        existing.ApplyTransaction("Incoming", 500m, "Other");
        _dailyRepo.GetAsync(AccountId, Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns(existing);

        var msg = new TransactionCreated(Guid.NewGuid(), AccountId, TransactionType.Incoming, 300m, TxDate);

        await _consumer.Consume(BuildContext(msg));

        await _dailyRepo.Received(1).UpsertAsync(
            Arg.Is<DailySummary>(s => s.TotalIncoming == 800m),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Consume_InvalidatesDailyCacheKey()
    {
        _dailyRepo.GetAsync(Arg.Any<Guid>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns((DailySummary?)null);

        var msg = new TransactionCreated(Guid.NewGuid(), AccountId, TransactionType.Incoming, 100m, TxDate);

        await _consumer.Consume(BuildContext(msg));

        var expectedKey = $"daily:{AccountId}:2024-06-15";
        await _cache.Received(1).RemoveAsync(expectedKey, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Consume_InvalidatesMonthlyCacheKey()
    {
        _dailyRepo.GetAsync(Arg.Any<Guid>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns((DailySummary?)null);

        var msg = new TransactionCreated(Guid.NewGuid(), AccountId, TransactionType.Outgoing, 200m, TxDate);

        await _consumer.Consume(BuildContext(msg));

        var expectedKey = $"monthly:{AccountId}:2024:06";
        await _cache.Received(1).RemoveAsync(expectedKey, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Consume_OutgoingTransaction_UpdatesOutgoingTotal()
    {
        _dailyRepo.GetAsync(Arg.Any<Guid>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns((DailySummary?)null);

        var msg = new TransactionCreated(Guid.NewGuid(), AccountId, TransactionType.Outgoing, 450m, TxDate);

        await _consumer.Consume(BuildContext(msg));

        await _dailyRepo.Received(1).UpsertAsync(
            Arg.Is<DailySummary>(s => s.TotalOutgoing == 450m && s.TotalIncoming == 0m),
            Arg.Any<CancellationToken>());
    }
}

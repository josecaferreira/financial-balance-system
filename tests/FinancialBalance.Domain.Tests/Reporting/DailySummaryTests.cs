using FinancialBalance.Domain.Reporting;
using FluentAssertions;

namespace FinancialBalance.Domain.Tests.Reporting;

public class DailySummaryTests
{
    private static readonly Guid AccountId = Guid.NewGuid();
    private static readonly DateOnly Today = new(2024, 6, 15);

    [Fact]
    public void Create_InitializesWithZeroTotals()
    {
        var summary = DailySummary.Create(AccountId, Today);

        summary.AccountId.Should().Be(AccountId);
        summary.Date.Should().Be(Today);
        summary.TotalIncoming.Should().Be(0m);
        summary.TotalOutgoing.Should().Be(0m);
        summary.NetBalance.Should().Be(0m);
        summary.TransactionCount.Should().Be(0);
        summary.CategoryBreakdowns.Should().BeEmpty();
    }

    [Fact]
    public void ApplyTransaction_Incoming_UpdatesTotalsCorrectly()
    {
        var summary = DailySummary.Create(AccountId, Today);

        summary.ApplyTransaction("Incoming", 1000m, "Revenue");

        summary.TotalIncoming.Should().Be(1000m);
        summary.TotalOutgoing.Should().Be(0m);
        summary.NetBalance.Should().Be(1000m);
        summary.TransactionCount.Should().Be(1);
    }

    [Fact]
    public void ApplyTransaction_Outgoing_UpdatesTotalsCorrectly()
    {
        var summary = DailySummary.Create(AccountId, Today);

        summary.ApplyTransaction("Outgoing", 400m, "Rent");

        summary.TotalIncoming.Should().Be(0m);
        summary.TotalOutgoing.Should().Be(400m);
        summary.NetBalance.Should().Be(-400m);
        summary.TransactionCount.Should().Be(1);
    }

    [Fact]
    public void ApplyTransaction_MultipleCalls_AccumulatesTotals()
    {
        var summary = DailySummary.Create(AccountId, Today);

        summary.ApplyTransaction("Incoming", 3000m, "Revenue");
        summary.ApplyTransaction("Outgoing", 1200m, "Payroll");
        summary.ApplyTransaction("Outgoing", 300m, "Utility");

        summary.TotalIncoming.Should().Be(3000m);
        summary.TotalOutgoing.Should().Be(1500m);
        summary.NetBalance.Should().Be(1500m);
        summary.TransactionCount.Should().Be(3);
    }

    [Fact]
    public void ApplyTransaction_CaseInsensitiveType_HandledCorrectly()
    {
        var summary = DailySummary.Create(AccountId, Today);

        summary.ApplyTransaction("incoming", 500m, "Revenue");

        summary.TotalIncoming.Should().Be(500m);
        summary.TotalOutgoing.Should().Be(0m);
    }

    [Fact]
    public void ApplyTransaction_CreatesNewCategoryBreakdown_WhenCategoryIsNew()
    {
        var summary = DailySummary.Create(AccountId, Today);

        summary.ApplyTransaction("Incoming", 500m, "Revenue");

        summary.CategoryBreakdowns.Should().HaveCount(1);
        summary.CategoryBreakdowns.First().Category.Should().Be("Revenue");
        summary.CategoryBreakdowns.First().TotalIncoming.Should().Be(500m);
    }

    [Fact]
    public void ApplyTransaction_AccumulatesExistingCategoryBreakdown()
    {
        var summary = DailySummary.Create(AccountId, Today);

        summary.ApplyTransaction("Incoming", 500m, "Revenue");
        summary.ApplyTransaction("Incoming", 300m, "Revenue");

        summary.CategoryBreakdowns.Should().HaveCount(1);
        summary.CategoryBreakdowns.First().TotalIncoming.Should().Be(800m);
    }

    [Fact]
    public void ApplyTransaction_TracksSeparateCategoriesIndependently()
    {
        var summary = DailySummary.Create(AccountId, Today);

        summary.ApplyTransaction("Incoming", 1000m, "Revenue");
        summary.ApplyTransaction("Outgoing", 400m, "Rent");

        summary.CategoryBreakdowns.Should().HaveCount(2);
        summary.CategoryBreakdowns.First(c => c.Category == "Revenue").TotalIncoming.Should().Be(1000m);
        summary.CategoryBreakdowns.First(c => c.Category == "Rent").TotalOutgoing.Should().Be(400m);
    }

    [Fact]
    public void ReverseTransaction_Incoming_SubtractsFromTotals()
    {
        var summary = DailySummary.Create(AccountId, Today);
        summary.ApplyTransaction("Incoming", 1000m, "Revenue");

        summary.ReverseTransaction("Incoming", 1000m, "Revenue");

        summary.TotalIncoming.Should().Be(0m);
        summary.NetBalance.Should().Be(0m);
        summary.TransactionCount.Should().Be(0);
    }

    [Fact]
    public void ReverseTransaction_Outgoing_SubtractsFromOutgoing()
    {
        var summary = DailySummary.Create(AccountId, Today);
        summary.ApplyTransaction("Outgoing", 500m, "Rent");

        summary.ReverseTransaction("Outgoing", 500m, "Rent");

        summary.TotalOutgoing.Should().Be(0m);
        summary.NetBalance.Should().Be(0m);
    }

    [Fact]
    public void ReverseTransaction_CountDoesNotGoBelowZero()
    {
        var summary = DailySummary.Create(AccountId, Today);

        summary.ReverseTransaction("Incoming", 100m, "Revenue");

        summary.TransactionCount.Should().Be(0);
    }

    [Fact]
    public void ReverseTransaction_AlsoReversesCategoryBreakdown()
    {
        var summary = DailySummary.Create(AccountId, Today);
        summary.ApplyTransaction("Incoming", 800m, "Revenue");

        summary.ReverseTransaction("Incoming", 800m, "Revenue");

        summary.CategoryBreakdowns.First(c => c.Category == "Revenue").TotalIncoming.Should().Be(0m);
    }
}

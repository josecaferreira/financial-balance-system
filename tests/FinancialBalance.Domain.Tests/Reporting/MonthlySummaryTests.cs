using FinancialBalance.Domain.Reporting;
using FluentAssertions;

namespace FinancialBalance.Domain.Tests.Reporting;

public class MonthlySummaryTests
{
    private static readonly Guid AccountId = Guid.NewGuid();

    private static DailySummary BuildDaily(DateOnly date, decimal incoming, decimal outgoing, string category = "Revenue")
    {
        var summary = DailySummary.Create(AccountId, date);
        if (incoming > 0) summary.ApplyTransaction("Incoming", incoming, category);
        if (outgoing > 0) summary.ApplyTransaction("Outgoing", outgoing, category);
        return summary;
    }

    [Fact]
    public void ComputeFrom_WithNoDailySummaries_ReturnsZeroTotals()
    {
        var monthly = MonthlySummary.ComputeFrom(AccountId, 2024, 1, 0m, []);

        monthly.TotalIncoming.Should().Be(0m);
        monthly.TotalOutgoing.Should().Be(0m);
        monthly.NetBalance.Should().Be(0m);
        monthly.TransactionCount.Should().Be(0);
        monthly.CategoryBreakdowns.Should().BeEmpty();
    }

    [Fact]
    public void ComputeFrom_AggregatesAllDailyTotals()
    {
        var dailies = new[]
        {
            BuildDaily(new DateOnly(2024, 1, 1), 5000m, 1000m),
            BuildDaily(new DateOnly(2024, 1, 2), 3000m, 500m),
            BuildDaily(new DateOnly(2024, 1, 3), 0m, 800m)
        };

        var monthly = MonthlySummary.ComputeFrom(AccountId, 2024, 1, 0m, dailies);

        monthly.TotalIncoming.Should().Be(8000m);
        monthly.TotalOutgoing.Should().Be(2300m);
        monthly.NetBalance.Should().Be(5700m);
        monthly.TransactionCount.Should().Be(5); // day1=2, day2=2, day3=1 (outgoing only)
    }

    [Fact]
    public void ComputeFrom_ClosingBalance_IsOpeningPlusNet()
    {
        var dailies = new[]
        {
            BuildDaily(new DateOnly(2024, 1, 1), 2000m, 500m)
        };

        var monthly = MonthlySummary.ComputeFrom(AccountId, 2024, 1, 1000m, dailies);

        monthly.OpeningBalance.Should().Be(1000m);
        monthly.NetBalance.Should().Be(1500m);
        monthly.ClosingBalance.Should().Be(2500m);
    }

    [Fact]
    public void ComputeFrom_AggregateCategoryBreakdowns_AcrossDays()
    {
        var day1 = DailySummary.Create(AccountId, new DateOnly(2024, 1, 1));
        day1.ApplyTransaction("Incoming", 1000m, "Revenue");
        day1.ApplyTransaction("Outgoing", 300m, "Payroll");

        var day2 = DailySummary.Create(AccountId, new DateOnly(2024, 1, 2));
        day2.ApplyTransaction("Incoming", 500m, "Revenue");
        day2.ApplyTransaction("Outgoing", 200m, "Rent");

        var monthly = MonthlySummary.ComputeFrom(AccountId, 2024, 1, 0m, [day1, day2]);

        monthly.CategoryBreakdowns.Should().HaveCount(3);
        monthly.CategoryBreakdowns.First(c => c.Category == "Revenue").TotalIncoming.Should().Be(1500m);
        monthly.CategoryBreakdowns.First(c => c.Category == "Payroll").TotalOutgoing.Should().Be(300m);
        monthly.CategoryBreakdowns.First(c => c.Category == "Rent").TotalOutgoing.Should().Be(200m);
    }

    [Fact]
    public void ComputeFrom_SetsCorrectAccountYearMonth()
    {
        var monthly = MonthlySummary.ComputeFrom(AccountId, 2024, 6, 0m, []);

        monthly.AccountId.Should().Be(AccountId);
        monthly.Year.Should().Be(2024);
        monthly.Month.Should().Be(6);
    }

    [Fact]
    public void ComputeFrom_WithNegativeNet_ClosingBalanceLessThanOpening()
    {
        var dailies = new[]
        {
            BuildDaily(new DateOnly(2024, 1, 1), 0m, 2000m)
        };

        var monthly = MonthlySummary.ComputeFrom(AccountId, 2024, 1, 5000m, dailies);

        monthly.NetBalance.Should().Be(-2000m);
        monthly.ClosingBalance.Should().Be(3000m);
    }
}

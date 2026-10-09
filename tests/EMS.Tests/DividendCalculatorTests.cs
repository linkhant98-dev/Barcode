using EMS.Domain.Calculations;
using Xunit;

namespace EMS.Tests;

public class DividendCalculatorTests
{
    [Fact]
    public void Calculate_OldSharesOnly_UsesFullDividendNoProration()
    {
        // Section 10.1 baseline example: 8% rate, MMK 10,000 capital value per share.
        var result = DividendCalculator.Calculate(
            oldShares: 1000, newShares: 0, eligibleDaysForNewShares: 0,
            capitalValuePerShare: 10000m, dividendPercentage: 8m,
            cashWithdrawal: 0, accountTransfer: 0, reinvestedAmount: 0);

        Assert.Equal(800_000m, result.OldShareDividend); // 1000 * 10000 * 8%
        Assert.Equal(0m, result.NewShareDividend);
        Assert.Equal(800_000m, result.TotalDividend);
        Assert.Equal(800_000m, result.OutstandingBalance);
    }

    [Fact]
    public void Calculate_NewSharesProratedByEligibleDays()
    {
        var result = DividendCalculator.Calculate(
            oldShares: 0, newShares: 1000, eligibleDaysForNewShares: 182,
            capitalValuePerShare: 10000m, dividendPercentage: 8m,
            cashWithdrawal: 0, accountTransfer: 0, reinvestedAmount: 0);

        var expected = 1000 * 10000m * 8m / 100m * 182m / 365m;
        Assert.Equal(expected, result.NewShareDividend);
    }

    [Fact]
    public void Calculate_OutstandingBalance_SubtractsAllSettlementMethods()
    {
        var result = DividendCalculator.Calculate(
            oldShares: 1000, newShares: 0, eligibleDaysForNewShares: 0,
            capitalValuePerShare: 10000m, dividendPercentage: 8m,
            cashWithdrawal: 300_000m, accountTransfer: 200_000m, reinvestedAmount: 100_000m);

        Assert.Equal(200_000m, result.OutstandingBalance); // 800,000 - 300,000 - 200,000 - 100,000
    }
}

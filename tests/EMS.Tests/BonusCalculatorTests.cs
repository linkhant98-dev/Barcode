using EMS.Domain.Calculations;
using Xunit;

namespace EMS.Tests;

public class BonusCalculatorTests
{
    [Fact]
    public void Calculate_SixToOneRatioWithRemainder_MatchesSpecExample()
    {
        // Section 9.1 baseline example: 6:1 ratio, MMK 5,000 cash bonus per remainder share.
        var result = BonusCalculator.Calculate(eligibleShares: 12500, bonusNumerator: 1, bonusDenominator: 6, cashBonusRatePerRemainderShare: 5000m);

        Assert.Equal(2083m, result.BonusShares);
        Assert.Equal(2m, result.RemainderShares); // 12500 % 6 = 2
        Assert.Equal(10000m, result.CashBonusAmount); // 2 * 5000
        Assert.Equal(14583m, result.NewTotalShares);
    }

    [Fact]
    public void Calculate_ExactMultiple_NoRemainderOrCashBonus()
    {
        var result = BonusCalculator.Calculate(eligibleShares: 12000, bonusNumerator: 1, bonusDenominator: 6, cashBonusRatePerRemainderShare: 5000m);

        Assert.Equal(2000m, result.BonusShares);
        Assert.Equal(0m, result.RemainderShares);
        Assert.Equal(0m, result.CashBonusAmount);
    }

    [Fact]
    public void Calculate_ZeroDenominator_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => BonusCalculator.Calculate(1000, 1, 0, 5000m));
    }

    [Fact]
    public void Calculate_NegativeShares_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => BonusCalculator.Calculate(-100, 1, 6, 5000m));
    }
}

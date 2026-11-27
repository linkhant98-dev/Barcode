namespace EMS.Domain.Calculations;

public record DividendCalculationResult(
    decimal OldShareDividend,
    decimal NewShareDividend,
    decimal TotalDividend,
    decimal OutstandingBalance);

/// <summary>
/// Section 10.1 formulas. Old shares are eligible for the full 365 days; new shares are prorated by
/// EligibleDays / 365. Dividend percentage and capital value per share are event parameters, never hard-coded.
/// </summary>
public static class DividendCalculator
{
    public static DividendCalculationResult Calculate(
        decimal oldShares,
        decimal newShares,
        int eligibleDaysForNewShares,
        decimal capitalValuePerShare,
        decimal dividendPercentage,
        decimal cashWithdrawal,
        decimal accountTransfer,
        decimal reinvestedAmount,
        decimal adjustmentAmount = 0m)
    {
        if (oldShares < 0 || newShares < 0)
            throw new ArgumentOutOfRangeException(nameof(oldShares), "Share counts cannot be negative.");

        var oldShareDividend = oldShares * capitalValuePerShare * dividendPercentage / 100m;
        var newShareDividend = newShares * capitalValuePerShare * dividendPercentage / 100m * eligibleDaysForNewShares / 365m;
        var totalDividend = oldShareDividend + newShareDividend;
        var outstanding = totalDividend - cashWithdrawal - accountTransfer - reinvestedAmount - adjustmentAmount;

        return new DividendCalculationResult(oldShareDividend, newShareDividend, totalDividend, outstanding);
    }
}

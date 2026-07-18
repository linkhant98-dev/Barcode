namespace EMS.Domain.Calculations;

public record BonusCalculationResult(
    decimal EligibleShares,
    decimal RawBonusEntitlement,
    decimal BonusShares,
    decimal RemainderShares,
    decimal CashBonusAmount,
    decimal NewTotalShares);

/// <summary>
/// Section 9.1 formulas. Ratio (numerator/denominator) and cash rate are always supplied parameters -
/// never hard-coded - per the spec's explicit instruction that the 6:1 / MMK 5,000 example must be configurable.
/// </summary>
public static class BonusCalculator
{
    public static BonusCalculationResult Calculate(
        decimal eligibleShares,
        int bonusNumerator,
        int bonusDenominator,
        decimal cashBonusRatePerRemainderShare)
    {
        if (bonusDenominator <= 0)
            throw new ArgumentOutOfRangeException(nameof(bonusDenominator), "Bonus denominator must be greater than zero.");
        if (eligibleShares < 0)
            throw new ArgumentOutOfRangeException(nameof(eligibleShares), "Eligible shares cannot be negative.");

        var rawEntitlement = eligibleShares * bonusNumerator / bonusDenominator;
        var bonusShares = Math.Floor(rawEntitlement);

        // "Current Shares mod Bonus Denominator, for a 1-for-N ratio" - remainder is computed against the
        // denominator (the eligible-share block size), matching the 6:1 example in the spec.
        var remainderShares = eligibleShares % bonusDenominator;
        var cashBonus = remainderShares * cashBonusRatePerRemainderShare;
        var newTotal = eligibleShares + bonusShares;

        return new BonusCalculationResult(eligibleShares, rawEntitlement, bonusShares, remainderShares, cashBonus, newTotal);
    }
}

namespace EMS.Application.CorporateActions;

public record CreateDividendEventRequest(
    string FinancialYear,
    DateOnly RecordDate,
    decimal DividendPercentage,
    decimal CapitalValuePerShare);

public record SettleDividendRequest(
    long DividendEntitlementId,
    decimal CashWithdrawal,
    decimal AccountTransfer,
    decimal ReinvestedAmount,
    string? SettlementReference);

/// <summary>Section 10 - dividend calculation, settlement tracking, and reinvestment linkage.</summary>
public interface IDividendService
{
    Task<long> CreateEventAsync(CreateDividendEventRequest request, CancellationToken ct = default);

    /// <summary>10.3 step 2 - calculates entitlement per shareholder using holdings and eligible dates.</summary>
    Task PreviewCalculationAsync(long dividendEventId, CancellationToken ct = default);

    Task SubmitAsync(long dividendEventId, CancellationToken ct = default);

    /// <summary>10.3 step 6 - creates payable balances after approval.</summary>
    Task PostApprovedAsync(long dividendEventId, CancellationToken ct = default);

    /// <summary>Settlement cannot exceed available balance; reinvestment creates a linked Issue Shares transaction (10.2, 10.3).</summary>
    Task SettleAsync(SettleDividendRequest request, CancellationToken ct = default);
}

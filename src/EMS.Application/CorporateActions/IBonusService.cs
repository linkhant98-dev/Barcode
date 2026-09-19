namespace EMS.Application.CorporateActions;

public record CreateBonusEventRequest(
    string FinancialYear,
    DateOnly RecordDate,
    int BonusNumerator,
    int BonusDenominator,
    decimal CashBonusRatePerRemainderShare,
    long ShareClassId);

/// <summary>Section 9 - preview calculation against eligible holdings, version, submit, and post on approval.</summary>
public interface IBonusService
{
    Task<long> CreateEventAsync(CreateBonusEventRequest request, CancellationToken ct = default);

    /// <summary>9.2 step 3 - runs the calculation against current holdings as of the record date and stores entitlement snapshots.</summary>
    Task PreviewCalculationAsync(long bonusEventId, CancellationToken ct = default);

    Task SubmitAsync(long bonusEventId, CancellationToken ct = default);

    /// <summary>9.2 step 7 - posts bonus share ledger entries and cash bonus obligations.</summary>
    Task PostApprovedAsync(long bonusEventId, CancellationToken ct = default);
}

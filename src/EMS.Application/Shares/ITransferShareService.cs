using EMS.Domain.Common;
using EMS.Domain.Shares;

namespace EMS.Application.Shares;

public record CreateTransferRequest(
    long FromShareholderId,
    long ToShareholderId,
    long ShareClassId,
    DateOnly TransferDate,
    decimal Quantity,
    TransferType TransferType,
    decimal CapitalAmount,
    decimal PremiumAmount,
    decimal CashAmount,
    decimal ChequeAmount,
    NonTradeReason? NonTradeReason,
    string? NonTradeReasonDetail);

/// <summary>Section 8 - Transfer Shares: validate seller balance/restrictions, submit, and post atomically on approval.</summary>
public interface ITransferShareService
{
    Task<ShareTransaction> CreateDraftAsync(CreateTransferRequest request, CancellationToken ct = default);
    Task SubmitAsync(long transactionId, CancellationToken ct = default);

    /// <summary>Atomically debits the seller and credits the buyer (8.1 step 7, 8.3).</summary>
    Task PostApprovedAsync(long transactionId, CancellationToken ct = default);
}

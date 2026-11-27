using EMS.Domain.Common;
using EMS.Domain.Shares;

namespace EMS.Application.Shares;

public record CreateIssueRequest(
    IssueApplyType ApplyType,
    long ShareholderId,
    long ShareClassId,
    DateOnly IssueDate,
    decimal NumberOfShares,
    decimal CapitalValuePerShare,
    decimal PremiumValuePerShare,
    decimal CashAmount,
    decimal ChequeAmount,
    string? ChequeNumber,
    DateOnly? ChequeDate,
    long? DividendEntitlementId);

/// <summary>Section 7 - Issue Shares: validate, calculate, submit for approval, and post on final approval.</summary>
public interface IIssueShareService
{
    Task<ShareTransaction> CreateDraftAsync(CreateIssueRequest request, CancellationToken ct = default);
    Task SubmitAsync(long transactionId, CancellationToken ct = default);

    /// <summary>Called by the workflow engine when the final approval step completes (IS-BR-08).</summary>
    Task PostApprovedAsync(long transactionId, CancellationToken ct = default);
}

using EMS.Application.Abstractions;
using EMS.Application.Shares;
using EMS.Application.Workflow;
using EMS.Domain.Common;
using EMS.Domain.Shares;
using EMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EMS.Infrastructure.Services;

/// <summary>Section 7 - Issue Shares business rules IS-BR-01..09.</summary>
public class IssueShareService : IIssueShareService
{
    private readonly EmsDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IReferenceNumberService _refNumbers;
    private readonly IWorkflowService _workflow;
    private readonly IShareLedgerService _ledger;
    private readonly IAuditService _audit;

    public IssueShareService(
        EmsDbContext db, ICurrentUserService currentUser, IReferenceNumberService refNumbers,
        IWorkflowService workflow, IShareLedgerService ledger, IAuditService audit)
    {
        _db = db;
        _currentUser = currentUser;
        _refNumbers = refNumbers;
        _workflow = workflow;
        _ledger = ledger;
        _audit = audit;
    }

    public async Task<ShareTransaction> CreateDraftAsync(CreateIssueRequest request, CancellationToken ct = default)
    {
        var shareholder = await _db.Shareholders.FindAsync([request.ShareholderId], ct)
            ?? throw new InvalidOperationException("Shareholder not found.");

        // IS-BR-01: only approved, active shareholders may receive shares.
        if (shareholder.Status != ShareholderStatus.Active || shareholder.KycStatus != KycResult.Approved)
            throw new InvalidOperationException("Only approved, active shareholders may receive shares.");

        // IS-BR-02: quantity and monetary values must be positive.
        if (request.NumberOfShares <= 0 || request.CapitalValuePerShare < 0 || request.PremiumValuePerShare < 0)
            throw new InvalidOperationException("Share quantity and monetary values must be positive.");

        // IS-BR-06: issue date cannot precede shareholder registration date.
        if (request.IssueDate < shareholder.RegistrationDate)
            throw new InvalidOperationException("Issue date cannot precede the shareholder's registration date.");

        // IS-BR-03: capital/premium/total are system-calculated.
        var capitalAmount = request.NumberOfShares * request.CapitalValuePerShare;
        var premiumAmount = request.NumberOfShares * request.PremiumValuePerShare;
        var totalAmount = capitalAmount + premiumAmount;

        // IS-BR-04: payment total must equal the subscription total.
        var paymentTotal = request.CashAmount + request.ChequeAmount;
        if (paymentTotal != totalAmount)
            throw new InvalidOperationException("Payment total must equal capital plus premium amount.");

        var transaction = new ShareTransaction
        {
            TransactionNo = await _refNumbers.NextAsync("IS", ct: ct),
            Type = ShareTransactionType.IssueShares,
            Status = WorkflowStatus.Draft,
            EffectiveDate = request.IssueDate,
            MakerUserId = _currentUser.UserId,
            TotalAmount = totalAmount,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = _currentUser.UserId,
            ShareIssue = new ShareIssue
            {
                ApplyType = request.ApplyType,
                ShareholderId = request.ShareholderId,
                ShareClassId = request.ShareClassId,
                NumberOfShares = request.NumberOfShares,
                CapitalValuePerShare = request.CapitalValuePerShare,
                PremiumValuePerShare = request.PremiumValuePerShare,
                CapitalAmount = capitalAmount,
                PremiumAmount = premiumAmount,
                TotalAmount = totalAmount,
                CashAmount = request.CashAmount,
                ChequeAmount = request.ChequeAmount,
                ChequeNumber = request.ChequeNumber,
                ChequeDate = request.ChequeDate,
                DividendEntitlementId = request.DividendEntitlementId
            }
        };

        _db.ShareTransactions.Add(transaction);
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("Create", "IS", nameof(ShareTransaction), transaction.TransactionNo, ct: ct);
        return transaction;
    }

    public async Task SubmitAsync(long transactionId, CancellationToken ct = default)
    {
        var transaction = await _db.ShareTransactions.Include(t => t.ShareIssue)
            .FirstOrDefaultAsync(t => t.Id == transactionId, ct)
            ?? throw new InvalidOperationException("Transaction not found.");

        transaction.Status = WorkflowStatus.PendingApproval;
        await _db.SaveChangesAsync(ct);

        var instance = await _workflow.SubmitForApprovalAsync(new SubmitForApprovalRequest(
            nameof(ShareTransaction), transaction.Id, transaction.TransactionNo, "IS",
            transaction.ShareIssue!.Shareholder?.ShareholderGroupId, transaction.ShareIssue.ShareClassId, transaction.TotalAmount), ct);

        transaction.ApprovalInstanceId = instance.Id;
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>IS-BR-08: approved issue posts immutable ledger entries and updates paid-up capital.</summary>
    public async Task PostApprovedAsync(long transactionId, CancellationToken ct = default)
    {
        var transaction = await _db.ShareTransactions.Include(t => t.ShareIssue)
            .FirstOrDefaultAsync(t => t.Id == transactionId, ct)
            ?? throw new InvalidOperationException("Transaction not found.");

        var issue = transaction.ShareIssue ?? throw new InvalidOperationException("Issue detail not found.");

        await _ledger.PostAsync(new PostLedgerEntryRequest(
            issue.ShareholderId, issue.ShareClassId, issue.NumberOfShares,
            issue.CapitalAmount, issue.PremiumAmount, transaction.TransactionNo,
            transaction.Id, null, null, transaction.EffectiveDate), ct);

        // IS-BR-07: certificate numbers must be unique and non-overlapping.
        var certificateNumber = await _refNumbers.NextCertificateNoAsync(ct);
        _db.ShareCertificates.Add(new ShareCertificate
        {
            CertificateNumber = certificateNumber,
            ShareholderId = issue.ShareholderId,
            ShareClassId = issue.ShareClassId,
            Quantity = issue.NumberOfShares,
            Status = CertificateStatus.PendingPrint,
            IssueTransactionId = transaction.Id,
            IssueDate = transaction.EffectiveDate,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = _currentUser.UserId
        });

        transaction.Status = WorkflowStatus.Completed;
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("Decision", "IS", nameof(ShareTransaction), transaction.TransactionNo,
            after: new { Posted = true, certificateNumber }, ct: ct);
    }
}

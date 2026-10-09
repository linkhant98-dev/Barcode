using EMS.Application.Abstractions;
using EMS.Application.Shares;
using EMS.Application.Workflow;
using EMS.Domain.Common;
using EMS.Domain.Shares;
using EMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace EMS.Infrastructure.Services;

/// <summary>Section 8 - Transfer Shares: trade/non-trade, seller-balance validation, atomic posting.</summary>
public class TransferShareService : ITransferShareService
{
    private readonly EmsDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IReferenceNumberService _refNumbers;
    private readonly IWorkflowService _workflow;
    private readonly IShareLedgerService _ledger;
    private readonly IAuditService _audit;

    public TransferShareService(
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

    public async Task<ShareTransaction> CreateDraftAsync(CreateTransferRequest request, CancellationToken ct = default)
    {
        // 8.3: transfer from and to cannot be the same shareholder.
        if (request.FromShareholderId == request.ToShareholderId)
            throw new InvalidOperationException("Transfer from and transfer to cannot be the same shareholder.");

        var from = await _db.Shareholders.FindAsync([request.FromShareholderId], ct)
            ?? throw new InvalidOperationException("Transfer-from shareholder not found.");
        var to = await _db.Shareholders.FindAsync([request.ToShareholderId], ct)
            ?? throw new InvalidOperationException("Transfer-to shareholder not found.");

        if (from.Status != ShareholderStatus.Active || to.Status != ShareholderStatus.Active)
            throw new InvalidOperationException("Both parties must be active, approved shareholders.");

        if (request.Quantity <= 0)
            throw new InvalidOperationException("Transfer quantity must be greater than zero.");

        var available = await _ledger.GetCurrentBalanceAsync(request.FromShareholderId, request.ShareClassId, null, ct);
        if (request.Quantity > available)
            throw new InvalidOperationException("Transfer quantity exceeds available transferable shares.");

        var totalConsideration = request.CapitalAmount + request.PremiumAmount;

        var transaction = new ShareTransaction
        {
            TransactionNo = await _refNumbers.NextAsync("TS", ct: ct),
            Type = ShareTransactionType.TransferShares,
            Status = WorkflowStatus.Draft,
            EffectiveDate = request.TransferDate,
            MakerUserId = _currentUser.UserId,
            TotalAmount = totalConsideration,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = _currentUser.UserId,
            ShareTransfer = new ShareTransfer
            {
                FromShareholderId = request.FromShareholderId,
                ToShareholderId = request.ToShareholderId,
                ShareClassId = request.ShareClassId,
                Quantity = request.Quantity,
                TransferType = request.TransferType,
                CapitalAmount = request.TransferType == TransferType.Trade ? request.CapitalAmount : 0,
                PremiumAmount = request.TransferType == TransferType.Trade ? request.PremiumAmount : 0,
                TotalConsideration = request.TransferType == TransferType.Trade ? totalConsideration : 0,
                CashAmount = request.TransferType == TransferType.Trade ? request.CashAmount : 0,
                ChequeAmount = request.TransferType == TransferType.Trade ? request.ChequeAmount : 0,
                NonTradeReason = request.TransferType == TransferType.NonTrade ? request.NonTradeReason : null,
                NonTradeReasonDetail = request.TransferType == TransferType.NonTrade ? request.NonTradeReasonDetail : null
            }
        };

        if (request.TransferType == TransferType.NonTrade && request.NonTradeReason is null)
            throw new InvalidOperationException("A reason is required for non-trade transfers.");

        _db.ShareTransactions.Add(transaction);
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("Create", "TS", nameof(ShareTransaction), transaction.TransactionNo, ct: ct);
        return transaction;
    }

    public async Task SubmitAsync(long transactionId, CancellationToken ct = default)
    {
        var transaction = await _db.ShareTransactions.Include(t => t.ShareTransfer)
            .FirstOrDefaultAsync(t => t.Id == transactionId, ct)
            ?? throw new InvalidOperationException("Transaction not found.");

        transaction.Status = WorkflowStatus.PendingApproval;
        await _db.SaveChangesAsync(ct);

        var instance = await _workflow.SubmitForApprovalAsync(new SubmitForApprovalRequest(
            nameof(ShareTransaction), transaction.Id, transaction.TransactionNo, "TS",
            null, transaction.ShareTransfer!.ShareClassId, transaction.TotalAmount), ct);

        transaction.ApprovalInstanceId = instance.Id;
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>8.1 step 7 - atomically reduces seller holdings and increases buyer holdings under a DB transaction.</summary>
    public async Task PostApprovedAsync(long transactionId, CancellationToken ct = default)
    {
        var transaction = await _db.ShareTransactions.Include(t => t.ShareTransfer)
            .FirstOrDefaultAsync(t => t.Id == transactionId, ct)
            ?? throw new InvalidOperationException("Transaction not found.");

        var transfer = transaction.ShareTransfer ?? throw new InvalidOperationException("Transfer detail not found.");

        var available = await _ledger.GetCurrentBalanceAsync(transfer.FromShareholderId, transfer.ShareClassId, null, ct);
        if (transfer.Quantity > available)
            throw new InvalidOperationException("Transfer quantity exceeds available shares at posting time.");

        IDbContextTransaction? dbTransaction = _db.Database.CurrentTransaction is null
            ? await _db.Database.BeginTransactionAsync(ct)
            : null;

        try
        {
            await _ledger.PostAsync(new PostLedgerEntryRequest(
                transfer.FromShareholderId, transfer.ShareClassId, -transfer.Quantity,
                -transfer.CapitalAmount, -transfer.PremiumAmount, transaction.TransactionNo,
                transaction.Id, null, null, transaction.EffectiveDate), ct);

            await _ledger.PostAsync(new PostLedgerEntryRequest(
                transfer.ToShareholderId, transfer.ShareClassId, transfer.Quantity,
                transfer.CapitalAmount, transfer.PremiumAmount, transaction.TransactionNo,
                transaction.Id, null, null, transaction.EffectiveDate), ct);

            transaction.Status = WorkflowStatus.Completed;
            await _db.SaveChangesAsync(ct);

            if (dbTransaction is not null)
                await dbTransaction.CommitAsync(ct);
        }
        catch
        {
            if (dbTransaction is not null)
                await dbTransaction.RollbackAsync(ct);
            throw;
        }
        finally
        {
            dbTransaction?.Dispose();
        }

        await _audit.LogAsync("Decision", "TS", nameof(ShareTransaction), transaction.TransactionNo, after: new { Posted = true }, ct: ct);
    }
}

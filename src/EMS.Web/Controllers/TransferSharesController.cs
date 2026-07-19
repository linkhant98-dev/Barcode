using EMS.Application.Abstractions;
using EMS.Application.Shares;
using EMS.Infrastructure.Persistence;
using EMS.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EMS.Web.Controllers;

/// <summary>Section 8 - Transfer Shares.</summary>
[Authorize]
public class TransferSharesController : Controller
{
    private readonly EmsDbContext _db;
    private readonly ITransferShareService _transferService;
    private readonly IPermissionService _permissions;

    public TransferSharesController(EmsDbContext db, ITransferShareService transferService, IPermissionService permissions)
    {
        _db = db;
        _transferService = transferService;
        _permissions = permissions;
    }

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        ViewData["Title"] = "Transfer Shares";
        ViewData["Breadcrumb"] = new List<(string, string?)> { ("Dashboard", "/"), ("Share Operations", null), ("Transfer Shares", null) };

        var transactions = await _db.ShareTransactions
            .Include(t => t.ShareTransfer)!.ThenInclude(x => x!.FromShareholder)
            .Include(t => t.ShareTransfer)!.ThenInclude(x => x!.ToShareholder)
            .Where(t => t.Type == Domain.Common.ShareTransactionType.TransferShares)
            .OrderByDescending(t => t.Id).Take(100).ToListAsync(ct);

        return View(transactions);
    }

    [HttpGet]
    public async Task<IActionResult> Create(CancellationToken ct)
    {
        ViewData["Title"] = "New Transfer Shares";
        ViewData["Breadcrumb"] = new List<(string, string?)> { ("Dashboard", "/"), ("Transfer Shares", Url.Action("Index")), ("New", null) };
        await PopulateLookupsAsync(ct);
        return View(new CreateTransferViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateTransferViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            await PopulateLookupsAsync(ct);
            return View(model);
        }

        try
        {
            var transaction = await _transferService.CreateDraftAsync(new CreateTransferRequest(
                model.FromShareholderId, model.ToShareholderId, model.ShareClassId, model.TransferDate,
                model.Quantity, model.TransferType, model.CapitalAmount, model.PremiumAmount,
                model.CashAmount, model.ChequeAmount, model.NonTradeReason, model.NonTradeReasonDetail), ct);

            return RedirectToAction(nameof(Details), new { id = transaction.Id });
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            await PopulateLookupsAsync(ct);
            return View(model);
        }
    }

    public async Task<IActionResult> Details(long id, CancellationToken ct)
    {
        var transaction = await _db.ShareTransactions
            .Include(t => t.ShareTransfer)!.ThenInclude(x => x!.FromShareholder)
            .Include(t => t.ShareTransfer)!.ThenInclude(x => x!.ToShareholder)
            .Include(t => t.ShareTransfer)!.ThenInclude(x => x!.ShareClass)
            .FirstOrDefaultAsync(t => t.Id == id, ct);
        if (transaction is null) return NotFound();

        ViewData["Title"] = transaction.TransactionNo;
        ViewData["Breadcrumb"] = new List<(string, string?)> { ("Dashboard", "/"), ("Transfer Shares", Url.Action("Index")), (transaction.TransactionNo, null) };

        ViewBag.ApprovalInstance = transaction.ApprovalInstanceId is null
            ? null
            : await _db.ApprovalInstances.Include(a => a.Steps).FirstOrDefaultAsync(a => a.Id == transaction.ApprovalInstanceId, ct);

        return View(transaction);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Submit(long id, CancellationToken ct)
    {
        if (!await _permissions.CurrentUserHasPermissionAsync(Permissions.SubmitTransferShares, ct))
            return Forbid();

        await _transferService.SubmitAsync(id, ct);
        TempData["Success"] = "Transfer submitted for approval.";
        return RedirectToAction(nameof(Details), new { id });
    }

    private async Task PopulateLookupsAsync(CancellationToken ct)
    {
        ViewBag.Shareholders = await _db.Shareholders.Include(s => s.Person).Include(s => s.Corporate)
            .Where(s => s.Status == Domain.Common.ShareholderStatus.Active)
            .Select(s => new { s.Id, Label = s.ShareholderNo + " - " + (s.Corporate != null ? s.Corporate.LegalNameEn : s.Person!.NameEn) })
            .ToListAsync(ct);
        ViewBag.ShareClasses = await _db.ShareClasses.Where(c => c.IsActive).ToListAsync(ct);
    }
}

using EMS.Application.Shares;
using EMS.Infrastructure.Persistence;
using EMS.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EMS.Web.Controllers;

/// <summary>Section 7 - Issue Shares.</summary>
[Authorize]
public class IssueSharesController : Controller
{
    private readonly EmsDbContext _db;
    private readonly IIssueShareService _issueService;

    public IssueSharesController(EmsDbContext db, IIssueShareService issueService)
    {
        _db = db;
        _issueService = issueService;
    }

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        ViewData["Title"] = "Issue Shares";
        ViewData["Breadcrumb"] = new List<(string, string?)> { ("Dashboard", "/"), ("Share Operations", null), ("Issue Shares", null) };

        var transactions = await _db.ShareTransactions.Include(t => t.ShareIssue).ThenInclude(i => i!.Shareholder)
            .Where(t => t.Type == Domain.Common.ShareTransactionType.IssueShares)
            .OrderByDescending(t => t.Id).Take(100).ToListAsync(ct);

        return View(transactions);
    }

    [HttpGet]
    public async Task<IActionResult> Create(long? shareholderId, CancellationToken ct)
    {
        ViewData["Title"] = "New Issue Shares";
        ViewData["Breadcrumb"] = new List<(string, string?)> { ("Dashboard", "/"), ("Issue Shares", Url.Action("Index")), ("New", null) };
        await PopulateLookupsAsync(ct);
        return View(new CreateIssueViewModel { ShareholderId = shareholderId ?? 0 });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateIssueViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            await PopulateLookupsAsync(ct);
            return View(model);
        }

        try
        {
            var transaction = await _issueService.CreateDraftAsync(new CreateIssueRequest(
                model.ApplyType, model.ShareholderId, model.ShareClassId, model.IssueDate,
                model.NumberOfShares, model.CapitalValuePerShare, model.PremiumValuePerShare,
                model.CashAmount, model.ChequeAmount, model.ChequeNumber, model.ChequeDate, null), ct);

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
            .Include(t => t.ShareIssue)!.ThenInclude(i => i!.Shareholder)!.ThenInclude(s => s!.Person)
            .Include(t => t.ShareIssue)!.ThenInclude(i => i!.ShareClass)
            .FirstOrDefaultAsync(t => t.Id == id, ct);
        if (transaction is null) return NotFound();

        ViewData["Title"] = transaction.TransactionNo;
        ViewData["Breadcrumb"] = new List<(string, string?)> { ("Dashboard", "/"), ("Issue Shares", Url.Action("Index")), (transaction.TransactionNo, null) };

        ViewBag.ApprovalInstance = transaction.ApprovalInstanceId is null
            ? null
            : await _db.ApprovalInstances.Include(a => a.Steps).FirstOrDefaultAsync(a => a.Id == transaction.ApprovalInstanceId, ct);

        return View(transaction);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Submit(long id, CancellationToken ct)
    {
        await _issueService.SubmitAsync(id, ct);
        TempData["Success"] = "Issue submitted for approval.";
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

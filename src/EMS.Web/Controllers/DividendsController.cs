using EMS.Application.Abstractions;
using EMS.Application.CorporateActions;
using EMS.Infrastructure.Persistence;
using EMS.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EMS.Web.Controllers;

/// <summary>Section 10 - Dividend Management.</summary>
[Authorize]
public class DividendsController : Controller
{
    private readonly EmsDbContext _db;
    private readonly IDividendService _dividendService;
    private readonly IPermissionService _permissions;

    public DividendsController(EmsDbContext db, IDividendService dividendService, IPermissionService permissions)
    {
        _db = db;
        _dividendService = dividendService;
        _permissions = permissions;
    }

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        ViewData["Title"] = "Dividends";
        ViewData["Breadcrumb"] = new List<(string, string?)> { ("Dashboard", "/"), ("Share Operations", null), ("Dividends", null) };
        var events = await _db.DividendEvents.OrderByDescending(d => d.Id).ToListAsync(ct);
        return View(events);
    }

    [HttpGet]
    public IActionResult Create()
    {
        ViewData["Title"] = "New Dividend Event";
        return View(new CreateDividendEventViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateDividendEventViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid) return View(model);

        var id = await _dividendService.CreateEventAsync(new CreateDividendEventRequest(
            model.FinancialYear, model.RecordDate, model.DividendPercentage, model.CapitalValuePerShare), ct);
        return RedirectToAction(nameof(Details), new { id });
    }

    public async Task<IActionResult> Details(long id, CancellationToken ct)
    {
        var dividendEvent = await _db.DividendEvents.Include(d => d.Entitlements).ThenInclude(e => e.Shareholder)
            .FirstOrDefaultAsync(d => d.Id == id, ct);
        if (dividendEvent is null) return NotFound();

        ViewData["Title"] = dividendEvent.DividendEventNo;
        ViewData["Breadcrumb"] = new List<(string, string?)> { ("Dashboard", "/"), ("Dividends", Url.Action("Index")), (dividendEvent.DividendEventNo, null) };
        ViewBag.ApprovalInstance = dividendEvent.ApprovalInstanceId is null
            ? null
            : await _db.ApprovalInstances.Include(a => a.Steps).FirstOrDefaultAsync(a => a.Id == dividendEvent.ApprovalInstanceId, ct);

        return View(dividendEvent);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Preview(long id, CancellationToken ct)
    {
        await _dividendService.PreviewCalculationAsync(id, ct);
        TempData["Success"] = "Calculation preview generated.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Submit(long id, CancellationToken ct)
    {
        if (!await _permissions.CurrentUserHasPermissionAsync(Permissions.SubmitDividend, ct))
            return Forbid();

        await _dividendService.SubmitAsync(id, ct);
        TempData["Success"] = "Dividend event submitted for approval.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Settle(long dividendEventId, long entitlementId, decimal cashWithdrawal, decimal accountTransfer, decimal reinvestedAmount, string? reference, CancellationToken ct)
    {
        try
        {
            await _dividendService.SettleAsync(new SettleDividendRequest(entitlementId, cashWithdrawal, accountTransfer, reinvestedAmount, reference), ct);
            TempData["Success"] = "Settlement recorded.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Details), new { id = dividendEventId });
    }
}

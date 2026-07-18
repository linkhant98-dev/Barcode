using EMS.Application.CorporateActions;
using EMS.Infrastructure.Persistence;
using EMS.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EMS.Web.Controllers;

/// <summary>Section 9 - Bonus Shares batch calculation and posting.</summary>
[Authorize]
public class BonusController : Controller
{
    private readonly EmsDbContext _db;
    private readonly IBonusService _bonusService;

    public BonusController(EmsDbContext db, IBonusService bonusService)
    {
        _db = db;
        _bonusService = bonusService;
    }

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        ViewData["Title"] = "Bonus Shares";
        ViewData["Breadcrumb"] = new List<(string, string?)> { ("Dashboard", "/"), ("Share Operations", null), ("Bonus Shares", null) };
        var events = await _db.BonusEvents.OrderByDescending(b => b.Id).ToListAsync(ct);
        return View(events);
    }

    [HttpGet]
    public async Task<IActionResult> Create(CancellationToken ct)
    {
        ViewData["Title"] = "New Bonus Event";
        ViewBag.ShareClasses = await _db.ShareClasses.Where(c => c.IsActive).ToListAsync(ct);
        return View(new CreateBonusEventViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateBonusEventViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.ShareClasses = await _db.ShareClasses.Where(c => c.IsActive).ToListAsync(ct);
            return View(model);
        }

        try
        {
            var id = await _bonusService.CreateEventAsync(new CreateBonusEventRequest(
                model.FinancialYear, model.RecordDate, model.BonusNumerator, model.BonusDenominator,
                model.CashBonusRatePerRemainderShare, model.ShareClassId), ct);
            return RedirectToAction(nameof(Details), new { id });
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            ViewBag.ShareClasses = await _db.ShareClasses.Where(c => c.IsActive).ToListAsync(ct);
            return View(model);
        }
    }

    public async Task<IActionResult> Details(long id, CancellationToken ct)
    {
        var bonusEvent = await _db.BonusEvents.Include(b => b.Entitlements).ThenInclude(e => e.Shareholder)
            .FirstOrDefaultAsync(b => b.Id == id, ct);
        if (bonusEvent is null) return NotFound();

        ViewData["Title"] = bonusEvent.BonusEventNo;
        ViewData["Breadcrumb"] = new List<(string, string?)> { ("Dashboard", "/"), ("Bonus Shares", Url.Action("Index")), (bonusEvent.BonusEventNo, null) };
        ViewBag.ApprovalInstance = bonusEvent.ApprovalInstanceId is null
            ? null
            : await _db.ApprovalInstances.Include(a => a.Steps).FirstOrDefaultAsync(a => a.Id == bonusEvent.ApprovalInstanceId, ct);

        return View(bonusEvent);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Preview(long id, CancellationToken ct)
    {
        await _bonusService.PreviewCalculationAsync(id, ct);
        TempData["Success"] = "Calculation preview generated.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Submit(long id, CancellationToken ct)
    {
        await _bonusService.SubmitAsync(id, ct);
        TempData["Success"] = "Bonus event submitted for approval.";
        return RedirectToAction(nameof(Details), new { id });
    }
}

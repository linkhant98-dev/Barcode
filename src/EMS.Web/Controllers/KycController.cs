using EMS.Application.Shareholders;
using EMS.Domain.Common;
using EMS.Infrastructure.Persistence;
using EMS.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EMS.Web.Controllers;

/// <summary>Section 6.4 - KYC officer work queue and decisions.</summary>
[Authorize]
public class KycController : Controller
{
    private readonly EmsDbContext _db;
    private readonly IKycService _kycService;

    public KycController(EmsDbContext db, IKycService kycService)
    {
        _db = db;
        _kycService = kycService;
    }

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        ViewData["Title"] = "KYC Work Queue";
        ViewData["Breadcrumb"] = new List<(string, string?)> { ("Dashboard", "/"), ("KYC Queue", null) };

        var cases = await _db.KycCases
            .Include(k => k.ShareholderApplication)
            .Where(k => k.Result == KycResult.Pending || k.Result == KycResult.PendingAdditionalInformation)
            .OrderBy(k => k.RequestedAtUtc)
            .ToListAsync(ct);

        return View(cases);
    }

    [HttpGet]
    public async Task<IActionResult> Decide(long id, CancellationToken ct)
    {
        var kycCase = await _db.KycCases.Include(k => k.ShareholderApplication).FirstOrDefaultAsync(k => k.Id == id, ct);
        if (kycCase is null) return NotFound();

        ViewData["Title"] = $"KYC Decision - {kycCase.ShareholderApplication?.ApplicationNo}";
        ViewData["Breadcrumb"] = new List<(string, string?)> { ("Dashboard", "/"), ("KYC Queue", Url.Action("Index")), (kycCase.ShareholderApplication?.ApplicationNo ?? "", null) };

        return View(kycCase);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Decide(long kycCaseId, KycResult result, RiskRating riskRating, string? remark, string? screeningReference, CancellationToken ct)
    {
        try
        {
            await _kycService.DecideAsync(new KycDecisionRequest(kycCaseId, result, riskRating, remark, screeningReference), ct);
            TempData["Success"] = result == KycResult.Approved
                ? "KYC approved. Shareholder registered."
                : $"KYC decision recorded: {result}.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Index));
    }
}

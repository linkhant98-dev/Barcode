using EMS.Infrastructure.Persistence;
using EMS.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EMS.Web.Controllers;

/// <summary>Section 11 - registered shareholder list and profile.</summary>
[Authorize]
public class ShareholdersController : Controller
{
    private readonly EmsDbContext _db;

    public ShareholdersController(EmsDbContext db) => _db = db;

    public async Task<IActionResult> Index(string? q, CancellationToken ct)
    {
        ViewData["Title"] = "Registered Shareholders";
        ViewData["Breadcrumb"] = new List<(string, string?)> { ("Dashboard", "/"), ("Registered Shareholders", null) };

        var query = _db.Shareholders.Include(s => s.Person).Include(s => s.Corporate).Include(s => s.ShareholderGroup).AsQueryable();

        if (!string.IsNullOrWhiteSpace(q))
        {
            query = query.Where(s => s.ShareholderNo.Contains(q)
                || (s.Person != null && s.Person.NameEn.Contains(q))
                || (s.Corporate != null && s.Corporate.LegalNameEn.Contains(q)));
        }

        ViewBag.Query = q;
        var shareholders = await query.OrderBy(s => s.ShareholderNo).Take(100).ToListAsync(ct);
        return View(shareholders);
    }

    public async Task<IActionResult> Details(long id, CancellationToken ct)
    {
        var shareholder = await _db.Shareholders
            .Include(s => s.Person).Include(s => s.Corporate).Include(s => s.ShareholderGroup)
            .Include(s => s.Addresses).Include(s => s.Contacts)
            .FirstOrDefaultAsync(s => s.Id == id, ct);
        if (shareholder is null) return NotFound();

        ViewData["Title"] = shareholder.ShareholderNo;
        ViewData["Breadcrumb"] = new List<(string, string?)> { ("Dashboard", "/"), ("Registered Shareholders", Url.Action("Index")), (shareholder.ShareholderNo, null) };

        var ledgerEntries = await _db.ShareLedgerEntries.Where(l => l.ShareholderId == id && !l.IsReversed)
            .OrderByDescending(l => l.LedgerId).ToListAsync(ct);
        var certificates = await _db.ShareCertificates.Where(c => c.ShareholderId == id).ToListAsync(ct);

        var vm = new ShareholderProfileViewModel
        {
            Shareholder = shareholder,
            TotalShares = ledgerEntries.Sum(l => l.QuantityDelta),
            PaidUpCapital = ledgerEntries.Sum(l => l.CapitalAmountDelta),
            LedgerEntries = ledgerEntries,
            Certificates = certificates
        };

        return View(vm);
    }
}

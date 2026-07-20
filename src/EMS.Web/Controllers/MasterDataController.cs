using EMS.Domain.Common;
using EMS.Domain.MasterData;
using EMS.Infrastructure.Persistence;
using EMS.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EMS.Web.Controllers;

/// <summary>Section 15.3 - master data list-and-drawer pattern shared across simple code/name master tables.</summary>
[Authorize(Roles = "System Administrator")]
public class MasterDataController : Controller
{
    private static readonly string[] Types =
        ["ShareholderGroup", "ShareClass", "Department", "BankBranch", "NrcPrefix", "DocumentType", "ReasonCode"];

    private readonly EmsDbContext _db;

    public MasterDataController(EmsDbContext db) => _db = db;

    public async Task<IActionResult> Index(string type = "ShareholderGroup", CancellationToken ct = default)
    {
        ViewData["Title"] = "Master Data";
        ViewData["Breadcrumb"] = new List<(string, string?)> { ("Dashboard", "/"), ("Administration", null), ("Master Data", null) };
        ViewBag.Types = Types;
        ViewBag.SelectedType = type;

        var items = await GetItemsAsync(type, ct);
        return View(items);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(MasterDataItemViewModel model, CancellationToken ct)
    {
        MasterDataEntity entity = model.Type switch
        {
            "ShareholderGroup" => new ShareholderGroup(),
            "ShareClass" => new ShareClass(),
            "Department" => new Department(),
            "BankBranch" => new BankBranch { Address = "-" },
            "NrcPrefix" => new NrcPrefix { StateRegion = "-", TownshipCode = "-", CitizenshipType = "Citizen" },
            "DocumentType" => new DocumentType(),
            "ReasonCode" => new ReasonCode { Category = "General" },
            _ => throw new InvalidOperationException("Unknown master data type.")
        };

        entity.Code = model.Code;
        entity.NameEn = model.NameEn;
        entity.NameMm = string.IsNullOrWhiteSpace(model.NameMm) ? model.NameEn : model.NameMm;
        entity.IsActive = true;
        entity.EffectiveFrom = DateOnly.FromDateTime(DateTime.UtcNow);
        entity.CreatedAtUtc = DateTime.UtcNow;
        entity.CreatedBy = User.Identity?.Name ?? "system";

        _db.Add(entity);
        await _db.SaveChangesAsync(ct);

        TempData["Success"] = $"{model.Type} '{model.Code}' added.";
        return RedirectToAction(nameof(Index), new { type = model.Type });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Deactivate(string type, long id, CancellationToken ct)
    {
        var entity = (await GetItemsAsync(type, ct)).FirstOrDefault(e => e.Id == id);
        if (entity is null) return NotFound();

        entity.IsActive = false;
        entity.EffectiveTo = DateOnly.FromDateTime(DateTime.UtcNow);
        await _db.SaveChangesAsync(ct);

        return RedirectToAction(nameof(Index), new { type });
    }

    private async Task<List<MasterDataEntity>> GetItemsAsync(string type, CancellationToken ct) => type switch
    {
        "ShareholderGroup" => (await _db.ShareholderGroups.OrderBy(x => x.Code).ToListAsync(ct)).Cast<MasterDataEntity>().ToList(),
        "ShareClass" => (await _db.ShareClasses.OrderBy(x => x.Code).ToListAsync(ct)).Cast<MasterDataEntity>().ToList(),
        "Department" => (await _db.Departments.OrderBy(x => x.Code).ToListAsync(ct)).Cast<MasterDataEntity>().ToList(),
        "BankBranch" => (await _db.BankBranches.OrderBy(x => x.Code).ToListAsync(ct)).Cast<MasterDataEntity>().ToList(),
        "NrcPrefix" => (await _db.NrcPrefixes.OrderBy(x => x.Code).ToListAsync(ct)).Cast<MasterDataEntity>().ToList(),
        "DocumentType" => (await _db.DocumentTypes.OrderBy(x => x.Code).ToListAsync(ct)).Cast<MasterDataEntity>().ToList(),
        "ReasonCode" => (await _db.ReasonCodes.OrderBy(x => x.Code).ToListAsync(ct)).Cast<MasterDataEntity>().ToList(),
        _ => []
    };
}

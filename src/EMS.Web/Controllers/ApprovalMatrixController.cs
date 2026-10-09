using EMS.Domain.Workflow;
using EMS.Infrastructure.Persistence;
using EMS.Infrastructure.Seed;
using EMS.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace EMS.Web.Controllers;

/// <summary>Section 15.2 - configurable approval matrix (module, sequence, approver role).</summary>
[Authorize(Roles = "System Administrator")]
public class ApprovalMatrixController : Controller
{
    private readonly EmsDbContext _db;

    public ApprovalMatrixController(EmsDbContext db) => _db = db;

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        ViewData["Title"] = "Approval Matrix";
        ViewData["Breadcrumb"] = new List<(string, string?)> { ("Dashboard", "/"), ("Administration", null), ("Approval Matrix", null) };

        var rules = await _db.ApprovalMatrixRules.Where(r => r.IsActive)
            .OrderBy(r => r.Module).ThenBy(r => r.Sequence).ToListAsync(ct);

        return View(rules);
    }

    [HttpGet]
    public IActionResult Create()
    {
        ViewData["Title"] = "New Approval Matrix Rule";
        ViewBag.Roles = new SelectList(DbSeeder.Roles);
        return View(new CreateApprovalMatrixRuleViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateApprovalMatrixRuleViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.Roles = new SelectList(DbSeeder.Roles);
            return View(model);
        }

        _db.ApprovalMatrixRules.Add(new ApprovalMatrixRule
        {
            Module = model.Module,
            Sequence = model.Sequence,
            StepName = model.StepName,
            ApproverRole = model.ApproverRole,
            IsMandatory = model.IsMandatory,
            StageMode = Domain.Common.ApprovalStageMode.Sequential,
            EffectiveFrom = model.EffectiveFrom,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = User.Identity?.Name ?? "system"
        });

        await _db.SaveChangesAsync(ct);
        TempData["Success"] = "Approval matrix rule added.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Deactivate(long id, CancellationToken ct)
    {
        var rule = await _db.ApprovalMatrixRules.FindAsync([id], ct);
        if (rule is null) return NotFound();

        rule.IsActive = false;
        rule.EffectiveTo = DateOnly.FromDateTime(DateTime.UtcNow);
        await _db.SaveChangesAsync(ct);
        return RedirectToAction(nameof(Index));
    }
}

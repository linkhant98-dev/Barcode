using EMS.Application.Workflow;
using EMS.Infrastructure.Identity;
using EMS.Infrastructure.Persistence;
using EMS.Infrastructure.Seed;
using EMS.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace EMS.Web.Controllers;

/// <summary>Section 11.1 - temporary delegation of approval authority.</summary>
[Authorize]
public class DelegationsController : Controller
{
    private readonly EmsDbContext _db;
    private readonly IDelegationService _delegationService;
    private readonly UserManager<ApplicationUser> _userManager;

    public DelegationsController(EmsDbContext db, IDelegationService delegationService, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _delegationService = delegationService;
        _userManager = userManager;
    }

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        ViewData["Title"] = "Delegations";
        ViewData["Breadcrumb"] = new List<(string, string?)> { ("Dashboard", "/"), ("Workflow", null), ("Delegations", null) };

        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "";
        var delegations = await _db.Delegations
            .Where(d => d.FromUserId == userId)
            .OrderByDescending(d => d.Id)
            .ToListAsync(ct);

        var userNames = await _userManager.Users.ToDictionaryAsync(u => u.Id, u => u.FullName, ct);
        ViewBag.UserNames = userNames;

        return View(delegations);
    }

    [HttpGet]
    public IActionResult Create()
    {
        ViewData["Title"] = "New Delegation";
        ViewBag.Roles = new SelectList(DbSeeder.Roles);
        ViewBag.Users = new SelectList(_userManager.Users.ToList(), "Id", "FullName");
        return View(new CreateDelegationViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateDelegationViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.Roles = new SelectList(DbSeeder.Roles);
            ViewBag.Users = new SelectList(_userManager.Users.ToList(), "Id", "FullName");
            return View(model);
        }

        try
        {
            await _delegationService.CreateAsync(new CreateDelegationRequest(
                model.ToUserId, model.ApproverRole, model.StartDate, model.EndDate, model.Reason), ct);
            TempData["Success"] = "Delegation created.";
            return RedirectToAction(nameof(Index));
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            ViewBag.Roles = new SelectList(DbSeeder.Roles);
            ViewBag.Users = new SelectList(_userManager.Users.ToList(), "Id", "FullName");
            return View(model);
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> End(long id, CancellationToken ct)
    {
        await _delegationService.EndAsync(id, ct);
        TempData["Success"] = "Delegation ended.";
        return RedirectToAction(nameof(Index));
    }
}

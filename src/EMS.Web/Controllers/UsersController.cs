using EMS.Domain.Common;
using EMS.Infrastructure.Identity;
using EMS.Infrastructure.Seed;
using EMS.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace EMS.Web.Controllers;

/// <summary>Section 15.1 - Users and Roles administration.</summary>
[Authorize(Roles = "System Administrator")]
public class UsersController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<ApplicationRole> _roleManager;

    public UsersController(UserManager<ApplicationUser> userManager, RoleManager<ApplicationRole> roleManager)
    {
        _userManager = userManager;
        _roleManager = roleManager;
    }

    public async Task<IActionResult> Index()
    {
        ViewData["Title"] = "Users and Roles";
        ViewData["Breadcrumb"] = new List<(string, string?)> { ("Dashboard", "/"), ("Administration", null), ("Users and Roles", null) };

        var users = _userManager.Users.OrderBy(u => u.UserName).ToList();
        var rows = new List<(ApplicationUser User, IList<string> Roles)>();
        foreach (var user in users)
            rows.Add((user, await _userManager.GetRolesAsync(user)));

        return View(rows);
    }

    [HttpGet]
    public IActionResult Create()
    {
        ViewData["Title"] = "New User";
        ViewBag.Roles = new SelectList(DbSeeder.Roles);
        return View(new CreateUserViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateUserViewModel model)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.Roles = new SelectList(DbSeeder.Roles);
            return View(model);
        }

        var user = new ApplicationUser
        {
            UserName = model.Email, Email = model.Email, EmailConfirmed = true,
            FullName = model.FullName, Status = UserStatus.Active
        };

        var result = await _userManager.CreateAsync(user, model.Password);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors) ModelState.AddModelError(string.Empty, error.Description);
            ViewBag.Roles = new SelectList(DbSeeder.Roles);
            return View(model);
        }

        await _userManager.AddToRoleAsync(user, model.Role);
        TempData["Success"] = "User created.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleStatus(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user is null) return NotFound();

        user.Status = user.Status == UserStatus.Active ? UserStatus.Disabled : UserStatus.Active;
        user.LockoutEnd = user.Status == UserStatus.Disabled ? DateTimeOffset.MaxValue : null;
        await _userManager.UpdateAsync(user);

        return RedirectToAction(nameof(Index));
    }
}

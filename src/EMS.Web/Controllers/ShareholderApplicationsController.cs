using EMS.Application.Abstractions;
using EMS.Application.Shareholders;
using EMS.Domain.Common;
using EMS.Infrastructure.Persistence;
using EMS.Web.Models;
using EMS.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EMS.Web.Controllers;

/// <summary>Section 6.1 - Shareholder Application create/edit/submit.</summary>
[Authorize]
public class ShareholderApplicationsController : Controller
{
    private readonly EmsDbContext _db;
    private readonly IShareholderApplicationService _applicationService;
    private readonly IPermissionService _permissions;

    public ShareholderApplicationsController(EmsDbContext db, IShareholderApplicationService applicationService, IPermissionService permissions)
    {
        _db = db;
        _applicationService = applicationService;
        _permissions = permissions;
    }

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        ViewData["Title"] = "Shareholder Applications";
        ViewData["Breadcrumb"] = new List<(string, string?)> { ("Dashboard", "/"), ("Shareholder Applications", null) };

        var applications = await _db.ShareholderApplications
            .OrderByDescending(a => a.Id)
            .Take(100)
            .ToListAsync(ct);

        return View(applications);
    }

    /// <summary>Exports the full application pipeline (not just the 100-row on-screen preview) as CSV.</summary>
    public async Task<IActionResult> ExportCsv(CancellationToken ct)
    {
        var applications = await _db.ShareholderApplications.OrderByDescending(a => a.Id).ToListAsync(ct);
        var headers = new[] { "Application ID", "Applicant", "Type", "Stage", "Submitted" };
        var rows = applications.Select(a => (IReadOnlyList<object?>)new object?[]
        {
            a.ApplicationNo, a.Type == ApplicantType.Corporate ? a.LegalNameEn : a.NameEn, a.Type.ToString(),
            a.Status.ToString(), a.SubmittedDate?.ToString("dd MMM yyyy") ?? "—"
        });

        var bytes = CsvExportHelper.Build(headers, rows);
        return File(bytes, "text/csv", $"Shareholder-Applications-{DateTime.UtcNow:yyyyMMdd-HHmmss}.csv");
    }

    [HttpGet]
    public async Task<IActionResult> Create(CancellationToken ct)
    {
        ViewData["Title"] = "New Shareholder Application";
        ViewData["Breadcrumb"] = new List<(string, string?)> { ("Dashboard", "/"), ("Shareholder Applications", Url.Action("Index")), ("New", null) };
        ViewBag.Groups = await _db.ShareholderGroups.Where(g => g.IsActive).ToListAsync(ct);
        return View(new CreateApplicationViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateApplicationViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.Groups = await _db.ShareholderGroups.Where(g => g.IsActive).ToListAsync(ct);
            return View(model);
        }

        var application = await _applicationService.CreateDraftAsync(new CreateApplicationRequest(model.Type, model.ShareholderGroupId), ct);
        return RedirectToAction(nameof(Edit), new { id = application.Id });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(long id, CancellationToken ct)
    {
        var application = await _db.ShareholderApplications
            .Include(a => a.JointHolders).Include(a => a.KycCase)
            .Include(a => a.Directors).Include(a => a.AuthorizedSigners).Include(a => a.BeneficialOwners)
            .FirstOrDefaultAsync(a => a.Id == id, ct);
        if (application is null) return NotFound();

        ViewData["Title"] = application.ApplicationNo;
        ViewData["Breadcrumb"] = new List<(string, string?)> { ("Dashboard", "/"), ("Shareholder Applications", Url.Action("Index")), (application.ApplicationNo, null) };
        ViewBag.Groups = await _db.ShareholderGroups.Where(g => g.IsActive).ToListAsync(ct);

        var vm = new EditApplicationViewModel
        {
            Id = application.Id,
            ApplicationNo = application.ApplicationNo,
            Type = application.Type,
            Status = application.Status.ToString(),
            ShareholderGroupId = application.ShareholderGroupId,
            NameEn = application.NameEn,
            NameMm = application.NameMm,
            DateOfBirth = application.DateOfBirth,
            FatherName = application.FatherName,
            NrcPrefixCode = application.NrcPrefixCode,
            NrcNumber = application.NrcNumber,
            LegalNameEn = application.LegalNameEn,
            RegistrationNumber = application.RegistrationNumber,
            CorporateRegistrationDate = application.CorporateRegistrationDate,
            LegalForm = application.LegalForm,
            TaxIdentifier = application.TaxIdentifier,
            DateOfIncorporation = application.DateOfIncorporation,
            TypeOfInstitution = application.TypeOfInstitution,
            NatureOfBusiness = application.NatureOfBusiness,
            SourceOfFund = application.SourceOfFund,
            PaidUpCapital = application.PaidUpCapital,
            BoardResolutionDate = application.BoardResolutionDate,
            AddressLine1 = application.AddressLine1,
            Township = application.Township,
            City = application.City,
            StateRegion = application.StateRegion,
            Mobile = application.Mobile,
            Email = application.Email,
            JointHolders = application.JointHolders.Select(j => new ApplicationJointHolderRow
            {
                NameEn = j.NameEn,
                NrcNumber = j.NrcNumber,
                OwnershipPercentage = j.OwnershipPercentage
            }).ToList(),
            Directors = Pad(application.Directors.Select(d => new ApplicationDirectorRow
            {
                Name = d.Name,
                NrcNumber = d.NrcNumber,
                ResidentialAddress = d.ResidentialAddress
            }).ToList(), 3),
            AuthorizedSigners = Pad(application.AuthorizedSigners.Select(a => new ApplicationAuthorizedSignerRow
            {
                Name = a.Name,
                NrcNumber = a.NrcNumber,
                Designation = a.Designation
            }).ToList(), 3),
            BeneficialOwners = Pad(application.BeneficialOwners.Select(o => new ApplicationBeneficialOwnerRow
            {
                OwnerName = o.OwnerName,
                NrcNumber = o.NrcNumber,
                OwnershipPercentage = o.OwnershipPercentage
            }).ToList(), 3)
        };

        return View(vm);
    }

    /// <summary>Blueprint 4.1.3.3 Categories 3/4/6 rows can "add new row with add row button"; the web form
    /// renders a handful of blank slots up front instead (simpler and more robust than JS list-index
    /// management), and blank rows are dropped again on save (see MapToEntity).</summary>
    private static List<T> Pad<T>(List<T> rows, int minCount) where T : new()
    {
        while (rows.Count < minCount) rows.Add(new T());
        return rows;
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveDraft(EditApplicationViewModel model, CancellationToken ct)
    {
        var application = await _db.ShareholderApplications
            .Include(a => a.Directors).Include(a => a.AuthorizedSigners).Include(a => a.BeneficialOwners)
            .FirstOrDefaultAsync(a => a.Id == model.Id, ct);
        if (application is null) return NotFound();

        MapToEntity(model, application);
        await _applicationService.SaveDraftAsync(application, ct);

        TempData["Success"] = "Draft saved.";
        return RedirectToAction(nameof(Edit), new { id = model.Id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Submit(long id, CancellationToken ct)
    {
        if (!await _permissions.CurrentUserHasPermissionAsync(Permissions.SubmitShareholderApplication, ct))
            return Forbid();

        try
        {
            await _applicationService.SubmitAsync(id, ct);
            TempData["Success"] = "Application submitted to the KYC queue.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Edit), new { id });
    }

    private static void MapToEntity(EditApplicationViewModel model, Domain.Applications.ShareholderApplication entity)
    {
        entity.ShareholderGroupId = model.ShareholderGroupId;
        entity.NameEn = model.NameEn;
        entity.NameMm = model.NameMm;
        entity.DateOfBirth = model.DateOfBirth;
        entity.FatherName = model.FatherName;
        entity.NrcPrefixCode = model.NrcPrefixCode;
        entity.NrcNumber = model.NrcNumber;
        entity.LegalNameEn = model.LegalNameEn;
        entity.RegistrationNumber = model.RegistrationNumber;
        entity.CorporateRegistrationDate = model.CorporateRegistrationDate;
        entity.LegalForm = model.LegalForm;
        entity.TaxIdentifier = model.TaxIdentifier;
        entity.DateOfIncorporation = model.DateOfIncorporation;
        entity.TypeOfInstitution = model.TypeOfInstitution;
        entity.NatureOfBusiness = model.NatureOfBusiness;
        entity.SourceOfFund = model.SourceOfFund;
        entity.PaidUpCapital = model.PaidUpCapital;
        entity.BoardResolutionDate = model.BoardResolutionDate;
        entity.AddressLine1 = model.AddressLine1;
        entity.Township = model.Township;
        entity.City = model.City;
        entity.StateRegion = model.StateRegion;
        entity.Mobile = model.Mobile;
        entity.Email = model.Email;

        // Corporate sub-tables: replace-in-place from the posted rows, dropping blank slots (Pad above).
        entity.Directors.Clear();
        foreach (var d in model.Directors.Where(d => !string.IsNullOrWhiteSpace(d.Name)))
            entity.Directors.Add(new Domain.Applications.ApplicationDirector { Name = d.Name, NrcNumber = d.NrcNumber, ResidentialAddress = d.ResidentialAddress });

        entity.AuthorizedSigners.Clear();
        foreach (var a in model.AuthorizedSigners.Where(a => !string.IsNullOrWhiteSpace(a.Name)))
            entity.AuthorizedSigners.Add(new Domain.Applications.ApplicationAuthorizedSigner { Name = a.Name, NrcNumber = a.NrcNumber, Designation = a.Designation });

        entity.BeneficialOwners.Clear();
        foreach (var o in model.BeneficialOwners.Where(o => !string.IsNullOrWhiteSpace(o.OwnerName)))
            entity.BeneficialOwners.Add(new Domain.Applications.ApplicationBeneficialOwner { OwnerName = o.OwnerName, NrcNumber = o.NrcNumber, OwnershipPercentage = o.OwnershipPercentage });
    }
}

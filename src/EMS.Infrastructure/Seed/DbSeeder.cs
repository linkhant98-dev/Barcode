using EMS.Domain.Common;
using EMS.Domain.MasterData;
using EMS.Domain.Reporting;
using EMS.Domain.Shareholders;
using EMS.Domain.Shares;
using EMS.Domain.Workflow;
using EMS.Infrastructure.Identity;
using EMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EMS.Infrastructure.Seed;

/// <summary>
/// Idempotent startup seed: roles, an initial administrator, baseline master data, the default approval
/// matrix (11.1), the report catalogue (13.7/13.8), and a small demo dataset so the dashboard is not empty
/// on first run. Safe to call on every startup.
/// </summary>
public static class DbSeeder
{
    public static readonly string[] Roles =
    [
        "System Administrator",
        "Investor Relations Maker",
        "Investor Relations Checker",
        "KYC Officer",
        "DGM Approver",
        "Legal Approver",
        "DMD Approver",
        "Managing Director Approver",
        "CEO Approver",
        "Vice Chairman Approver",
        "Board Approver",
        "CBM Recording User",
        "Auditor",
        "Report Viewer"
    ];

    public static async Task SeedAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<EmsDbContext>();

        var roleManager = services.GetRequiredService<RoleManager<ApplicationRole>>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();

        foreach (var role in Roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new ApplicationRole(role) { DescriptionEn = role });
        }

        if (!await db.RolePermissions.AnyAsync())
        {
            // Section 12.2 default grants. System Administrator bypasses all checks in PermissionService, so
            // it is deliberately not seeded here (nothing to toggle off for the superuser).
            var grants = new List<(string Role, string Permission)>();
            void Grant(string role, params string[] permissions)
            {
                foreach (var p in permissions) grants.Add((role, p));
            }

            Grant("Investor Relations Maker",
                Application.Abstractions.Permissions.SubmitShareholderApplication,
                Application.Abstractions.Permissions.SubmitIssueShares,
                Application.Abstractions.Permissions.SubmitTransferShares,
                Application.Abstractions.Permissions.SubmitBonusShares,
                Application.Abstractions.Permissions.SubmitDividend,
                Application.Abstractions.Permissions.ReportsView);

            Grant("Investor Relations Checker", Application.Abstractions.Permissions.ReportsView, Application.Abstractions.Permissions.ReportsExport);
            Grant("KYC Officer", Application.Abstractions.Permissions.ReportsView);

            foreach (var approverRole in new[]
                     {
                         "DGM Approver", "Legal Approver", "DMD Approver", "Managing Director Approver",
                         "CEO Approver", "Vice Chairman Approver", "Board Approver", "CBM Recording User"
                     })
            {
                Grant(approverRole, Application.Abstractions.Permissions.ReportsView);
            }

            Grant("Auditor",
                Application.Abstractions.Permissions.ReportsView,
                Application.Abstractions.Permissions.ReportsExport,
                Application.Abstractions.Permissions.ReportsViewSensitiveData);

            Grant("Report Viewer", Application.Abstractions.Permissions.ReportsView, Application.Abstractions.Permissions.ReportsExport);

            db.RolePermissions.AddRange(grants.Select(g => new EMS.Domain.Security.RolePermission { RoleName = g.Role, PermissionKey = g.Permission }));
            await db.SaveChangesAsync();
        }

        const string adminEmail = "admin@ems.local";
        if (await userManager.FindByEmailAsync(adminEmail) is null)
        {
            var admin = new ApplicationUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                EmailConfirmed = true,
                FullName = "System Administrator",
                Status = UserStatus.Active
            };
            var result = await userManager.CreateAsync(admin, "Passw0rd!123");
            if (result.Succeeded)
                await userManager.AddToRoleAsync(admin, "System Administrator");
        }

        if (!await db.ShareholderGroups.AnyAsync())
        {
            var groups = new[] { "BOD", "Staff", "Management", "Public Company", "Cooperative", "Personal" }
                .Select(name => new ShareholderGroup { Code = name.ToUpperInvariant().Replace(" ", "_"), NameEn = name, NameMm = name, EffectiveFrom = new DateOnly(2020, 1, 1) })
                .ToList();
            db.ShareholderGroups.AddRange(groups);
        }

        if (!await db.ShareClasses.AnyAsync())
        {
            db.ShareClasses.Add(new ShareClass { Code = "ORD", NameEn = "Ordinary Shares", NameMm = "Ordinary Shares", EffectiveFrom = new DateOnly(2020, 1, 1), CarriesCertificate = true });
        }

        if (!await db.Departments.AnyAsync())
        {
            db.Departments.Add(new Department { Code = "IR", NameEn = "Investor Relations", NameMm = "Investor Relations", EffectiveFrom = new DateOnly(2020, 1, 1) });
        }

        if (!await db.BankBranches.AnyAsync())
        {
            db.BankBranches.Add(new BankBranch { Code = "HO", NameEn = "CB Bank Head Office", NameMm = "CB Bank Head Office", Address = "Yangon", EffectiveFrom = new DateOnly(2020, 1, 1) });
        }

        if (!await db.NrcPrefixes.AnyAsync())
        {
            db.NrcPrefixes.Add(new NrcPrefix { Code = "12/YAKANA", NameEn = "Yangon", NameMm = "Yangon", StateRegion = "Yangon", TownshipCode = "YAKANA", CitizenshipType = "Citizen", EffectiveFrom = new DateOnly(2020, 1, 1) });
        }

        if (!await db.DocumentTypes.AnyAsync())
        {
            string[] docs = ["NRC Copy", "Photo", "Address Evidence", "Bank Evidence", "Board Resolution", "Company Registration"];
            db.DocumentTypes.AddRange(docs.Select(d => new DocumentType { Code = d.Replace(" ", "_").ToUpperInvariant(), NameEn = d, NameMm = d, IsMandatoryForKyc = true, EffectiveFrom = new DateOnly(2020, 1, 1) }));
        }

        if (!await db.ReasonCodes.AnyAsync())
        {
            string[] reasons = ["Incomplete Documents", "Failed Screening", "Data Mismatch", "Duplicate Application", "Business Decision"];
            db.ReasonCodes.AddRange(reasons.Select(r => new ReasonCode { Code = r.Replace(" ", "_").ToUpperInvariant(), NameEn = r, NameMm = r, Category = "Rejection", EffectiveFrom = new DateOnly(2020, 1, 1) }));
        }

        await db.SaveChangesAsync();

        if (!await db.ApprovalMatrixRules.AnyAsync())
        {
            // 11.1 baseline sequence: DGM, Legal, DGM, DMD, Managing Director, CEO, Vice Chairman, Board of Directors, (CBM conditional).
            string[] steps = ["DGM", "Legal", "DMD", "Managing Director", "CEO", "Vice Chairman", "Board of Directors"];
            string[] roleMap = ["DGM Approver", "Legal Approver", "DMD Approver", "Managing Director Approver", "CEO Approver", "Vice Chairman Approver", "Board Approver"];
            string[] modules = ["SA", "IS", "TS", "BS", "DS"];

            foreach (var module in modules)
            {
                for (var i = 0; i < steps.Length; i++)
                {
                    db.ApprovalMatrixRules.Add(new ApprovalMatrixRule
                    {
                        Module = module,
                        Sequence = i + 1,
                        StepName = steps[i],
                        ApproverRole = roleMap[i],
                        IsMandatory = true,
                        StageMode = ApprovalStageMode.Sequential,
                        EffectiveFrom = new DateOnly(2020, 1, 1),
                        IsActive = true,
                        CreatedAtUtc = DateTime.UtcNow,
                        CreatedBy = "system"
                    });
                }
            }

            await db.SaveChangesAsync();
        }

        if (!await db.ReportDefinitions.AnyAsync())
        {
            (string Code, string NameEn, string Category)[] reports =
            [
                ("RPT-001", "Shareholders List", "Shareholder Reports"),
                ("RPT-002", "Shareholders Group Summary", "Shareholder Reports"),
                ("RPT-003", "Shares and Certificate Record", "Share and Capital Reports"),
                ("RPT-004", "Paid-up Capital Record", "Share and Capital Reports"),
                ("RPT-005", "Transfer Record", "Share and Capital Reports"),
                ("RPT-006", "Bonus Shares", "Share and Capital Reports"),
                ("RPT-007", "Cash Bonus Shares Summary", "Share and Capital Reports"),
                ("RPT-008", "Dividend Summary", "Dividend Reports"),
                ("RPT-009", "Dividend by Person", "Dividend Reports"),
                ("RPT-010", "Dividend Record by Year", "Dividend Reports"),
                ("RPT-011", "Approval History", "Operational Reports"),
                ("RPT-012", "Pending Approval Aging", "Operational Reports"),
                ("RPT-013", "KYC Status and Aging", "Operational Reports"),
                ("RPT-014", "Audit Trail", "Operational Reports"),
                ("RPT-015", "Reconciliation Exceptions", "Operational Reports"),
                ("RPT-016", "User Access Review", "Operational Reports"),
            ];

            db.ReportDefinitions.AddRange(reports.Select(r => new ReportDefinition
            {
                ReportCode = r.Code,
                NameEn = r.NameEn,
                NameMm = r.NameEn,
                Category = r.Category,
                RequiredPermission = $"Reports.{r.Code.Replace("-", "")}.View"
            }));

            await db.SaveChangesAsync();
        }

        await SeedDemoTransactionsAsync(db);
    }

    /// <summary>A handful of demo shareholders/postings so DB-01..DB-05 and RPT screens render non-empty on first run.</summary>
    private static async Task SeedDemoTransactionsAsync(EmsDbContext db)
    {
        if (await db.Shareholders.AnyAsync()) return;

        var group = await db.ShareholderGroups.FirstAsync();
        var bodGroup = await db.ShareholderGroups.FirstOrDefaultAsync(g => g.Code == "BOD") ?? group;
        var staffGroup = await db.ShareholderGroups.FirstOrDefaultAsync(g => g.Code == "STAFF") ?? group;
        var shareClass = await db.ShareClasses.FirstAsync();

        var demo = new[]
        {
            new { No = "SH-250000001", Name = "Daw Khin Mya", Group = bodGroup, Shares = 12500m, Capital = 12500m * 10000m },
            new { No = "SH-250000002", Name = "U Aung Aung", Group = staffGroup, Shares = 2000m, Capital = 2000m * 10000m },
            new { No = "SH-250000003", Name = "Ma Su Su Win", Group = staffGroup, Shares = 4200m, Capital = 4200m * 10000m },
        };

        foreach (var d in demo)
        {
            var shareholder = new Shareholder
            {
                ShareholderNo = d.No,
                Type = ApplicantType.Personal,
                ShareholderGroupId = d.Group.Id,
                Status = ShareholderStatus.Active,
                RegistrationDate = new DateOnly(2024, 1, 10),
                KycStatus = KycResult.Approved,
                KycApprovalDate = new DateOnly(2024, 1, 10),
                CreatedAtUtc = DateTime.UtcNow,
                CreatedBy = "system",
                Person = new Person { NameEn = d.Name, NameMm = d.Name, DateOfBirth = new DateOnly(1985, 1, 1), FatherName = "-", NrcNumber = "12/YAKANA(N)000000" }
            };
            db.Shareholders.Add(shareholder);
            await db.SaveChangesAsync();

            db.ShareLedgerEntries.Add(new ShareLedgerEntry
            {
                ShareholderId = shareholder.Id,
                ShareClassId = shareClass.Id,
                SourceReference = "OPENING-BALANCE",
                QuantityDelta = d.Shares,
                CapitalAmountDelta = d.Capital,
                RunningQuantityBalance = d.Shares,
                RunningPaidUpCapital = d.Capital,
                EffectiveDate = new DateOnly(2024, 1, 10),
                PostedAtUtc = DateTime.UtcNow,
                PostedByUserId = "system"
            });

            db.ShareCertificates.Add(new EMS.Domain.Shares.ShareCertificate
            {
                CertificateNumber = $"CERT-2024-{shareholder.Id:D6}",
                ShareholderId = shareholder.Id,
                ShareClassId = shareClass.Id,
                Quantity = d.Shares,
                Status = Domain.Common.CertificateStatus.Active,
                IssueDate = new DateOnly(2024, 1, 10),
                CreatedAtUtc = DateTime.UtcNow,
                CreatedBy = "system"
            });
        }

        await db.SaveChangesAsync();
    }
}

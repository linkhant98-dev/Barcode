using EMS.Domain.Applications;
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

    /// <summary>Seeds roles, the admin user, and master/reference data. <paramref name="seedDemoData"/> controls
    /// the demo shareholders and their sample activity - unnecessary for integration tests, which only need a
    /// fast, deterministic schema plus login credentials.</summary>
    public static async Task SeedAsync(IServiceProvider services, bool seedDemoData = true)
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

        if (!seedDemoData) return;

        await SeedDemoTransactionsAsync(db);
        await SeedSampleActivityAsync(db);
    }

    /// <summary>Five demo shareholders spanning five of the six shareholder groups (for RPT-002 diversity),
    /// each with an opening ledger posting and a certificate, so DB-01..DB-05 and the register/certificate
    /// screens render non-empty on first run. Internal (not private) so EMS.Tests can call it directly.</summary>
    internal static async Task SeedDemoTransactionsAsync(EmsDbContext db)
    {
        if (await db.Shareholders.AnyAsync()) return;

        var groups = await db.ShareholderGroups.ToDictionaryAsync(g => g.Code, g => g);
        var fallback = groups.Values.First();
        var shareClass = await db.ShareClasses.FirstAsync();

        Shareholder Personal(string no, string name, ShareholderGroup group, DateOnly regDate) => new()
        {
            ShareholderNo = no,
            Type = ApplicantType.Personal,
            ShareholderGroupId = group.Id,
            Status = ShareholderStatus.Active,
            RegistrationDate = regDate,
            KycStatus = KycResult.Approved,
            KycApprovalDate = regDate,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = "system",
            Person = new Person { NameEn = name, NameMm = name, DateOfBirth = new DateOnly(1985, 1, 1), FatherName = "-", NrcNumber = "12/YAKANA(N)000000" }
        };

        Shareholder Corporate(string no, string name, ShareholderGroup group, DateOnly regDate, string legalForm) => new()
        {
            ShareholderNo = no,
            Type = ApplicantType.Corporate,
            ShareholderGroupId = group.Id,
            Status = ShareholderStatus.Active,
            RegistrationDate = regDate,
            KycStatus = KycResult.Approved,
            KycApprovalDate = regDate,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = "system",
            Corporate = new Corporate { LegalNameEn = name, LegalNameMm = name, RegistrationNumber = $"DICA-{no[^6..]}", RegistrationDate = regDate, LegalForm = legalForm, ContactPersonName = "U Aung Kyaw" }
        };

        var demo = new[]
        {
            (Shareholder: Personal("SH-250000001", "Daw Khin Mya", groups.GetValueOrDefault("BOD", fallback), new DateOnly(2024, 1, 10)), Shares: 12500m),
            (Shareholder: Personal("SH-250000002", "U Aung Aung", groups.GetValueOrDefault("STAFF", fallback), new DateOnly(2024, 2, 15)), Shares: 2000m),
            (Shareholder: Personal("SH-250000003", "Ma Su Su Win", groups.GetValueOrDefault("PERSONAL", fallback), new DateOnly(2024, 3, 20)), Shares: 800m),
            (Shareholder: Corporate("SH-250000004", "Golden Delta Trading Co., Ltd.", groups.GetValueOrDefault("PUBLIC_COMPANY", fallback), new DateOnly(2024, 4, 5), "Private Company Limited"), Shares: 9500m),
            (Shareholder: Corporate("SH-250000005", "Yangon Region Farmers Co-operative", groups.GetValueOrDefault("COOPERATIVE", fallback), new DateOnly(2024, 5, 12), "Co-operative"), Shares: 3000m),
        };

        var seq = 0;
        foreach (var (shareholder, shares) in demo)
        {
            var capital = shares * 10000m;
            db.Shareholders.Add(shareholder);
            await db.SaveChangesAsync();
            seq++;

            db.ShareLedgerEntries.Add(new ShareLedgerEntry
            {
                ShareholderId = shareholder.Id,
                ShareClassId = shareClass.Id,
                SourceReference = "OPENING-BALANCE",
                QuantityDelta = shares,
                CapitalAmountDelta = capital,
                RunningQuantityBalance = shares,
                RunningPaidUpCapital = capital,
                EffectiveDate = shareholder.RegistrationDate,
                PostedAtUtc = DateTime.UtcNow,
                PostedByUserId = "system"
            });

            db.ShareCertificates.Add(new ShareCertificate
            {
                CertificateNumber = $"CERT-2026-{seq:D6}",
                ShareholderId = shareholder.Id,
                ShareClassId = shareClass.Id,
                Quantity = shares,
                Status = CertificateStatus.Active,
                IssueDate = shareholder.RegistrationDate,
                CreatedAtUtc = DateTime.UtcNow,
                CreatedBy = "system"
            });
        }

        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Five Issue Shares top-ups, five Transfer Shares transactions, and five pipeline applications - enough
    /// rows for every list screen and the 16 statutory/operational reports to render with real, non-empty
    /// sample data without carrying a bank-scale (~20,000-row) register. Deterministic (fixed seed). Guarded
    /// on ShareTransactions so it only ever runs once. Internal so EMS.Tests can call it directly.
    /// </summary>
    internal static async Task SeedSampleActivityAsync(EmsDbContext db)
    {
        const int year = 2026;
        const decimal parValue = 10000m;

        if (await db.ShareTransactions.AnyAsync()) return;

        var rnd = new Random(20260719);
        var shareClass = await db.ShareClasses.FirstAsync();
        var nrcPrefix = await db.NrcPrefixes.FirstAsync();
        var personalGroup = (await db.ShareholderGroups.ToDictionaryAsync(g => g.Code, g => g)).GetValueOrDefault("PERSONAL")
            ?? await db.ShareholderGroups.FirstAsync();

        var shareholders = await db.Shareholders
            .Select(s => new { s.Id, s.RegistrationDate })
            .OrderBy(s => s.Id)
            .ToListAsync();
        if (shareholders.Count == 0) return;

        var runningBalance = new Dictionary<long, (decimal Qty, decimal Capital)>();
        foreach (var s in shareholders)
        {
            var last = await db.ShareLedgerEntries
                .Where(l => l.ShareholderId == s.Id)
                .OrderByDescending(l => l.LedgerId)
                .FirstAsync();
            runningBalance[s.Id] = (last.RunningQuantityBalance, last.RunningPaidUpCapital);
        }

        // Five Issue Shares top-ups, one per demo shareholder, so the Issue Shares list and RPT-004
        // (paid-up capital) show ongoing activity beyond the opening balance.
        var isSeq = 0;
        foreach (var s in shareholders.Take(5))
        {
            var topUp = 20 + rnd.Next(0, 300);
            var capitalAmount = topUp * parValue;
            isSeq++;
            var effectiveDate = new DateOnly(year, 1, 1).AddDays(rnd.Next(0, 200));

            db.ShareTransactions.Add(new ShareTransaction
            {
                TransactionNo = $"IS-{year}-{isSeq:D6}",
                Type = ShareTransactionType.IssueShares,
                Status = WorkflowStatus.Completed,
                EffectiveDate = effectiveDate,
                MakerUserId = "system",
                TotalAmount = capitalAmount,
                ShareIssue = new ShareIssue
                {
                    ApplyType = IssueApplyType.IssueShare,
                    ShareholderId = s.Id,
                    ShareClassId = shareClass.Id,
                    NumberOfShares = topUp,
                    CapitalValuePerShare = parValue,
                    PremiumValuePerShare = 0,
                    CapitalAmount = capitalAmount,
                    PremiumAmount = 0,
                    TotalAmount = capitalAmount,
                    CashAmount = capitalAmount
                }
            });

            var (prevQty, prevCapital) = runningBalance[s.Id];
            var newQty = prevQty + topUp;
            var newCapital = prevCapital + capitalAmount;
            runningBalance[s.Id] = (newQty, newCapital);

            db.ShareLedgerEntries.Add(new ShareLedgerEntry
            {
                ShareholderId = s.Id,
                ShareClassId = shareClass.Id,
                SourceReference = $"IS-{year}-{isSeq:D6}",
                QuantityDelta = topUp,
                CapitalAmountDelta = capitalAmount,
                RunningQuantityBalance = newQty,
                RunningPaidUpCapital = newCapital,
                EffectiveDate = effectiveDate,
                PostedAtUtc = DateTime.UtcNow,
                PostedByUserId = "system"
            });
        }

        await db.SaveChangesAsync();

        // Five Transfer Shares transactions - a round-robin among the demo shareholders (1->2, 2->3, ...),
        // each moving a slice of the "from" shareholder's current balance, so the Transfer Shares list and
        // RPT-005 show data.
        var tsSeq = 0;
        var ids = shareholders.Select(s => s.Id).ToList();
        for (var i = 0; i < Math.Min(5, ids.Count); i++)
        {
            var fromId = ids[i];
            var toId = ids[(i + 1) % ids.Count];
            if (fromId == toId) continue;

            var (fromQty, fromCapital) = runningBalance[fromId];
            var qty = Math.Floor(fromQty * (decimal)(0.05 + rnd.NextDouble() * 0.15));
            if (qty < 1) continue;

            var perShareCapital = fromQty > 0 ? fromCapital / fromQty : parValue;
            var movedCapital = qty * perShareCapital;
            tsSeq++;
            var effectiveDate = new DateOnly(year, 1, 1).AddDays(rnd.Next(0, 200));

            db.ShareTransactions.Add(new ShareTransaction
            {
                TransactionNo = $"TS-{year}-{tsSeq:D6}",
                Type = ShareTransactionType.TransferShares,
                Status = WorkflowStatus.Completed,
                EffectiveDate = effectiveDate,
                MakerUserId = "system",
                TotalAmount = movedCapital,
                ShareTransfer = new ShareTransfer
                {
                    FromShareholderId = fromId,
                    ToShareholderId = toId,
                    ShareClassId = shareClass.Id,
                    Quantity = qty,
                    TransferType = rnd.NextDouble() < 0.6 ? TransferType.Trade : TransferType.NonTrade,
                    CapitalAmount = movedCapital,
                    TotalConsideration = movedCapital
                }
            });

            runningBalance[fromId] = (fromQty - qty, fromCapital - movedCapital);
            db.ShareLedgerEntries.Add(new ShareLedgerEntry
            {
                ShareholderId = fromId,
                ShareClassId = shareClass.Id,
                SourceReference = $"TS-{year}-{tsSeq:D6}",
                QuantityDelta = -qty,
                CapitalAmountDelta = -movedCapital,
                RunningQuantityBalance = runningBalance[fromId].Qty,
                RunningPaidUpCapital = runningBalance[fromId].Capital,
                EffectiveDate = effectiveDate,
                PostedAtUtc = DateTime.UtcNow,
                PostedByUserId = "system"
            });

            var (toQty, toCapital) = runningBalance[toId];
            runningBalance[toId] = (toQty + qty, toCapital + movedCapital);
            db.ShareLedgerEntries.Add(new ShareLedgerEntry
            {
                ShareholderId = toId,
                ShareClassId = shareClass.Id,
                SourceReference = $"TS-{year}-{tsSeq:D6}",
                QuantityDelta = qty,
                CapitalAmountDelta = movedCapital,
                RunningQuantityBalance = runningBalance[toId].Qty,
                RunningPaidUpCapital = runningBalance[toId].Capital,
                EffectiveDate = effectiveDate,
                PostedAtUtc = DateTime.UtcNow,
                PostedByUserId = "system"
            });
        }

        await db.SaveChangesAsync();

        // Five pipeline applications at varying workflow stages, independent of the shareholders already
        // registered above, so the Applications list, KYC Queue and RPT-013 show variety.
        string[] maleGiven = ["Aung Aung", "Zaw Min", "Kyaw Kyaw", "Thura", "Htet Naing"];
        string[] femaleGiven = ["Khin Thazin", "Su Su Hlaing", "Ei Ei Phyo", "Thanda Aye", "Zin Mar Oo"];
        string[] townships = ["Bahan", "Kamayut", "Sanchaung", "Tarmwe", "Mingalar Taung Nyunt"];
        WorkflowStatus[] stages =
        [
            WorkflowStatus.Draft,
            WorkflowStatus.Submitted,
            WorkflowStatus.KycPending,
            WorkflowStatus.PendingApproval,
            WorkflowStatus.Approved
        ];

        var saSeq = 0;
        for (var i = 0; i < 5; i++)
        {
            var isFemale = i % 2 == 0;
            var given = isFemale ? femaleGiven[i] : maleGiven[i];
            var name = (isFemale ? "Daw " : "U ") + given;
            var stage = stages[i];
            saSeq++;

            db.ShareholderApplications.Add(new ShareholderApplication
            {
                ApplicationNo = $"SA-{year}-{saSeq:D6}",
                Type = ApplicantType.Personal,
                Status = stage,
                SubmittedDate = stage == WorkflowStatus.Draft ? null : new DateOnly(year, 1, 1).AddDays(rnd.Next(0, 200)),
                MakerUserId = "system",
                ShareholderGroupId = personalGroup.Id,
                NameEn = name,
                NameMm = name,
                DateOfBirth = new DateOnly(1970, 1, 1).AddDays(rnd.Next(0, 15000)),
                FatherName = "U " + maleGiven[(i + 2) % maleGiven.Length],
                NrcPrefixCode = nrcPrefix.Code,
                NrcNumber = $"(N){rnd.Next(0, 999999):D6}",
                AddressLine1 = $"No. {1 + rnd.Next(0, 200)}, Pyay Road",
                Township = townships[i % townships.Length],
                City = "Yangon",
                StateRegion = "Yangon Region",
                Mobile = $"09{rnd.Next(100000000, 999999999)}",
                Email = $"applicant{i + 1}@example.com",
                CreatedAtUtc = DateTime.UtcNow,
                CreatedBy = "system"
            });
        }

        await db.SaveChangesAsync();

        // Keep the live reference-number sequences ahead of everything seeded above, so the next real
        // submission through the UI never collides with a seeded number.
        async Task BumpSequenceAsync(string module, long lastValue)
        {
            var numSeq = await db.NumberSequences.FirstOrDefaultAsync(s => s.Module == module && s.Year == year);
            if (numSeq is null) db.NumberSequences.Add(new NumberSequence { Module = module, Year = year, LastValue = lastValue });
            else if (numSeq.LastValue < lastValue) numSeq.LastValue = lastValue;
        }

        await BumpSequenceAsync("IS", isSeq);
        await BumpSequenceAsync("TS", tsSeq);
        await BumpSequenceAsync("SA", saSeq);
        await db.SaveChangesAsync();
    }
}

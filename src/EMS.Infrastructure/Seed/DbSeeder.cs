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
        await SeedBulkRegisterAsync(db);
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

    /// <summary>
    /// A ~20,000-shareholder register (plus matching certificates, opening/top-up postings, transfers and
    /// pipeline applications) so the dashboard, the 16 statutory/operational reports and every list screen
    /// reflect a realistic bank-scale dataset rather than the 3-row demo above. Deterministic (fixed seed)
    /// and batched (SaveChanges every ~500 rows, not per row) so a first run completes in a reasonable time.
    /// Guarded so it only ever runs once.
    /// </summary>
    private static async Task SeedBulkRegisterAsync(EmsDbContext db)
    {
        const int bulkCount = 20000;
        const int batchSize = 500;
        const int year = 2026;
        const decimal parValue = 10000m;

        if (await db.Shareholders.CountAsync() >= bulkCount) return;

        var rnd = new Random(20260719);
        var groups = await db.ShareholderGroups.ToDictionaryAsync(g => g.Code, g => g);
        var shareClass = await db.ShareClasses.FirstAsync();
        var nrcPrefix = await db.NrcPrefixes.FirstAsync();

        string[] maleGiven = ["Aung Aung", "Zaw Min", "Kyaw Kyaw", "Thura", "Htet Naing", "Min Min", "Aung Ko", "Ye Htut", "Zaw Htet", "Kyaw Zin", "Aung Kyaw", "Than Htike", "Nay Lin", "Zaw Zaw", "Myo Min"];
        string[] femaleGiven = ["Khin Thazin", "Su Su Hlaing", "Ei Ei Phyo", "Thanda Aye", "Zin Mar Oo", "Yadanar Win", "Hnin Wutyi", "Thida Win", "Khin Myo Myat", "May Thu Kyaw", "Aye Aye Mon", "Cho Cho Win", "Nilar Kyaw"];
        string[] corpNames = ["Golden Delta Trading Co., Ltd.", "Shwe Myanmar Industries Co., Ltd.", "Yangon Star Logistics Ltd.", "Irrawaddy Agro Products Co., Ltd.", "Grand Ruby Construction Co., Ltd.", "Mandalay Textile Manufacturing Ltd.", "Ayeyarwady Rice Exporters Co., Ltd.", "Bagan Hospitality Group Ltd.", "Zawgyi Digital Services Co., Ltd.", "Thanlwin Mining Corporation Ltd.", "Diamond Crown Retail Co., Ltd.", "Everbright Logistics Ltd."];
        string[] coopNames = ["Yangon Region Farmers Co-operative", "Mandalay Region Weavers Co-operative", "Bago Region Rice Millers Co-operative", "Shan State Highland Growers Co-operative", "Ayeyarwady Delta Fishery Co-operative"];
        string[] townships = ["Bahan", "Kamayut", "Sanchaung", "Tarmwe", "Mingalar Taung Nyunt", "Dagon", "Yankin", "Insein", "Hlaing", "Thingangyun"];

        var runningBalance = new Dictionary<long, (decimal Qty, decimal Capital)>();
        var bulkShareholderIds = new List<long>();
        var certSeq = 0;
        var isSeq = 0;

        for (var batchStart = 0; batchStart < bulkCount; batchStart += batchSize)
        {
            var batchLen = Math.Min(batchSize, bulkCount - batchStart);
            var shareholders = new List<Shareholder>(batchLen);

            for (var i = batchStart; i < batchStart + batchLen; i++)
            {
                var r = rnd.NextDouble();
                var isCorp = r < 0.10;
                var isCoop = !isCorp && r < 0.13;
                var isStaff = !isCorp && !isCoop && r < 0.30;
                var group = isCorp ? groups["PUBLIC_COMPANY"] : isCoop ? groups["COOPERATIVE"] : isStaff ? groups["STAFF"] : groups["PERSONAL"];

                var shareholder = new Shareholder
                {
                    ShareholderNo = $"SH-26{i + 1:D7}",
                    Type = isCorp || isCoop ? ApplicantType.Corporate : ApplicantType.Personal,
                    ShareholderGroupId = group.Id,
                    Status = ShareholderStatus.Active,
                    RegistrationDate = new DateOnly(2020, 1, 1).AddDays(rnd.Next(0, 2000)),
                    KycStatus = KycResult.Approved,
                    KycApprovalDate = new DateOnly(2020, 1, 15),
                    CreatedAtUtc = DateTime.UtcNow,
                    CreatedBy = "system"
                };

                if (isCorp || isCoop)
                {
                    var name = (isCoop ? coopNames[i % coopNames.Length] + " No." + (1 + i % 50) : corpNames[i % corpNames.Length] + " (" + (100 + i % 900) + ")");
                    shareholder.Corporate = new Corporate
                    {
                        LegalNameEn = name,
                        LegalNameMm = name,
                        RegistrationNumber = $"DICA-{20000 + i}",
                        RegistrationDate = shareholder.RegistrationDate,
                        LegalForm = isCoop ? "Co-operative" : "Private Company Limited",
                        ContactPersonName = maleGiven[i % maleGiven.Length]
                    };
                }
                else
                {
                    var isFemale = rnd.NextDouble() < 0.5;
                    var given = isFemale ? femaleGiven[i % femaleGiven.Length] : maleGiven[i % maleGiven.Length];
                    var name = (isFemale ? "Daw " : "U ") + given;
                    shareholder.Person = new Person
                    {
                        NameEn = name,
                        NameMm = name,
                        DateOfBirth = new DateOnly(1960, 1, 1).AddDays(rnd.Next(0, 20000)),
                        Gender = isFemale ? Gender.Female : Gender.Male,
                        FatherName = "U " + maleGiven[(i + 3) % maleGiven.Length],
                        NrcPrefixCode = nrcPrefix.Code,
                        NrcNumber = $"(N){rnd.Next(0, 999999):D6}",
                        MaritalStatus = MaritalStatus.Married
                    };
                }

                shareholders.Add(shareholder);
            }

            db.Shareholders.AddRange(shareholders);
            await db.SaveChangesAsync();

            var certs = new List<ShareCertificate>();
            var ledgerEntries = new List<ShareLedgerEntry>();

            foreach (var shareholder in shareholders)
            {
                var sr = rnd.NextDouble();
                var shares = sr < 0.75 ? 10 + rnd.Next(0, 490) : sr < 0.95 ? 500 + rnd.Next(0, 2000) : 2500 + rnd.Next(0, 7500);
                var capital = shares * parValue;

                certSeq++;
                certs.Add(new ShareCertificate
                {
                    CertificateNumber = $"CERT-{year}-{certSeq:D6}",
                    ShareholderId = shareholder.Id,
                    ShareClassId = shareClass.Id,
                    Quantity = shares,
                    Status = CertificateStatus.Active,
                    IssueDate = shareholder.RegistrationDate,
                    CreatedAtUtc = DateTime.UtcNow,
                    CreatedBy = "system"
                });

                ledgerEntries.Add(new ShareLedgerEntry
                {
                    ShareholderId = shareholder.Id,
                    ShareClassId = shareClass.Id,
                    SourceReference = "SEED-OPENING-BALANCE",
                    QuantityDelta = shares,
                    CapitalAmountDelta = capital,
                    RunningQuantityBalance = shares,
                    RunningPaidUpCapital = capital,
                    EffectiveDate = shareholder.RegistrationDate,
                    PostedAtUtc = DateTime.UtcNow,
                    PostedByUserId = "system"
                });

                runningBalance[shareholder.Id] = (shares, capital);
                bulkShareholderIds.Add(shareholder.Id);
            }

            db.ShareCertificates.AddRange(certs);
            db.ShareLedgerEntries.AddRange(ledgerEntries);
            await db.SaveChangesAsync();
        }

        // A top-up Issue Shares transaction for roughly 1-in-7 shareholders, so the Issue Shares list and
        // RPT-004 (paid-up capital) show ongoing activity rather than only the opening balance.
        for (var batchStart = 0; batchStart < bulkShareholderIds.Count; batchStart += batchSize)
        {
            var batch = bulkShareholderIds.Skip(batchStart).Take(batchSize).ToList();
            var transactions = new List<ShareTransaction>();

            foreach (var shareholderId in batch)
            {
                if (shareholderId % 7 != 0) continue;

                var topUp = 20 + rnd.Next(0, 300);
                var capitalAmount = topUp * parValue;
                isSeq++;

                var tx = new ShareTransaction
                {
                    TransactionNo = $"IS-{year}-{isSeq:D6}",
                    Type = ShareTransactionType.IssueShares,
                    Status = WorkflowStatus.Completed,
                    EffectiveDate = new DateOnly(2026, 1, 1).AddDays(rnd.Next(0, 200)),
                    MakerUserId = "system",
                    TotalAmount = capitalAmount,
                    ShareIssue = new ShareIssue
                    {
                        ApplyType = IssueApplyType.IssueShare,
                        ShareholderId = shareholderId,
                        ShareClassId = shareClass.Id,
                        NumberOfShares = topUp,
                        CapitalValuePerShare = parValue,
                        PremiumValuePerShare = 0,
                        CapitalAmount = capitalAmount,
                        PremiumAmount = 0,
                        TotalAmount = capitalAmount,
                        CashAmount = capitalAmount
                    }
                };
                transactions.Add(tx);

                var (prevQty, prevCapital) = runningBalance[shareholderId];
                var newQty = prevQty + topUp;
                var newCapital = prevCapital + capitalAmount;
                runningBalance[shareholderId] = (newQty, newCapital);

                db.ShareLedgerEntries.Add(new ShareLedgerEntry
                {
                    ShareholderId = shareholderId,
                    ShareClassId = shareClass.Id,
                    SourceReference = tx.TransactionNo,
                    QuantityDelta = topUp,
                    CapitalAmountDelta = capitalAmount,
                    RunningQuantityBalance = newQty,
                    RunningPaidUpCapital = newCapital,
                    EffectiveDate = tx.EffectiveDate,
                    PostedAtUtc = DateTime.UtcNow,
                    PostedByUserId = "system"
                });
            }

            db.ShareTransactions.AddRange(transactions);
            await db.SaveChangesAsync();
        }

        // Transfer Shares - random pairs, each transfer moves a slice of the "from" shareholder's balance.
        const int transferCount = 3000;
        var tsSeq = 0;
        for (var batchStart = 0; batchStart < transferCount; batchStart += batchSize)
        {
            var batchLen = Math.Min(batchSize, transferCount - batchStart);
            var transactions = new List<ShareTransaction>();

            for (var i = 0; i < batchLen; i++)
            {
                var fromId = bulkShareholderIds[rnd.Next(bulkShareholderIds.Count)];
                var toId = bulkShareholderIds[rnd.Next(bulkShareholderIds.Count)];
                if (fromId == toId) continue;

                var (fromQty, fromCapital) = runningBalance[fromId];
                var qty = Math.Floor(fromQty * (decimal)(0.05 + rnd.NextDouble() * 0.15));
                if (qty < 1) continue;

                var perShareCapital = fromQty > 0 ? fromCapital / fromQty : parValue;
                var movedCapital = qty * perShareCapital;

                tsSeq++;
                var effectiveDate = new DateOnly(2026, 1, 1).AddDays(rnd.Next(0, 200));
                var tx = new ShareTransaction
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
                };
                transactions.Add(tx);

                runningBalance[fromId] = (fromQty - qty, fromCapital - movedCapital);
                db.ShareLedgerEntries.Add(new ShareLedgerEntry
                {
                    ShareholderId = fromId, ShareClassId = shareClass.Id, SourceReference = tx.TransactionNo,
                    QuantityDelta = -qty, CapitalAmountDelta = -movedCapital,
                    RunningQuantityBalance = runningBalance[fromId].Qty, RunningPaidUpCapital = runningBalance[fromId].Capital,
                    EffectiveDate = effectiveDate, PostedAtUtc = DateTime.UtcNow, PostedByUserId = "system"
                });

                var (toQty, toCapital) = runningBalance[toId];
                runningBalance[toId] = (toQty + qty, toCapital + movedCapital);
                db.ShareLedgerEntries.Add(new ShareLedgerEntry
                {
                    ShareholderId = toId, ShareClassId = shareClass.Id, SourceReference = tx.TransactionNo,
                    QuantityDelta = qty, CapitalAmountDelta = movedCapital,
                    RunningQuantityBalance = runningBalance[toId].Qty, RunningPaidUpCapital = runningBalance[toId].Capital,
                    EffectiveDate = effectiveDate, PostedAtUtc = DateTime.UtcNow, PostedByUserId = "system"
                });
            }

            db.ShareTransactions.AddRange(transactions);
            await db.SaveChangesAsync();
        }

        // Shareholder Applications pipeline - new prospective applicants at various workflow stages,
        // independent of the shareholders already registered above.
        const int applicationCount = 3000;
        WorkflowStatus[] stages =
        [
            WorkflowStatus.Draft, WorkflowStatus.Submitted, WorkflowStatus.KycPending, WorkflowStatus.KycApproved,
            WorkflowStatus.PendingApproval, WorkflowStatus.Approved, WorkflowStatus.Completed, WorkflowStatus.Rejected
        ];
        double[] stageWeights = [0.10, 0.12, 0.15, 0.10, 0.08, 0.10, 0.30, 0.05];
        var personalGroup = groups["PERSONAL"];
        var saSeq = 0;

        for (var batchStart = 0; batchStart < applicationCount; batchStart += batchSize)
        {
            var batchLen = Math.Min(batchSize, applicationCount - batchStart);
            var applications = new List<ShareholderApplication>(batchLen);

            for (var i = 0; i < batchLen; i++)
            {
                var isFemale = rnd.NextDouble() < 0.5;
                var given = isFemale ? femaleGiven[i % femaleGiven.Length] : maleGiven[i % maleGiven.Length];
                var name = (isFemale ? "Daw " : "U ") + given;

                var sr = rnd.NextDouble();
                var acc = 0.0;
                var stage = stages[^1];
                for (var s = 0; s < stages.Length; s++)
                {
                    acc += stageWeights[s];
                    if (sr <= acc) { stage = stages[s]; break; }
                }

                saSeq++;
                applications.Add(new ShareholderApplication
                {
                    ApplicationNo = $"SA-{year}-{saSeq:D6}",
                    Type = ApplicantType.Personal,
                    Status = stage,
                    SubmittedDate = stage == WorkflowStatus.Draft ? null : new DateOnly(2026, 1, 1).AddDays(rnd.Next(0, 200)),
                    MakerUserId = "system",
                    ShareholderGroupId = personalGroup.Id,
                    NameEn = name,
                    NameMm = name,
                    DateOfBirth = new DateOnly(1970, 1, 1).AddDays(rnd.Next(0, 15000)),
                    FatherName = "U " + maleGiven[(i + 5) % maleGiven.Length],
                    NrcPrefixCode = nrcPrefix.Code,
                    NrcNumber = $"(N){rnd.Next(0, 999999):D6}",
                    AddressLine1 = $"No. {1 + rnd.Next(0, 200)}, Pyay Road",
                    Township = townships[i % townships.Length],
                    City = "Yangon",
                    StateRegion = "Yangon Region",
                    Mobile = $"09{rnd.Next(100000000, 999999999)}",
                    Email = $"applicant{batchStart + i + 1}@example.com",
                    CreatedAtUtc = DateTime.UtcNow,
                    CreatedBy = "system"
                });
            }

            db.ShareholderApplications.AddRange(applications);
            await db.SaveChangesAsync();
        }

        // Keep the live reference-number sequences ahead of everything seeded above, so the next real
        // submission through the UI never collides with a seeded number.
        async Task BumpSequenceAsync(string module, long lastValue)
        {
            var seq = await db.NumberSequences.FirstOrDefaultAsync(s => s.Module == module && s.Year == year);
            if (seq is null) db.NumberSequences.Add(new NumberSequence { Module = module, Year = year, LastValue = lastValue });
            else if (seq.LastValue < lastValue) seq.LastValue = lastValue;
        }

        await BumpSequenceAsync("SH", bulkCount);
        await BumpSequenceAsync("CERT", certSeq);
        await BumpSequenceAsync("IS", isSeq);
        await BumpSequenceAsync("TS", tsSeq);
        await BumpSequenceAsync("SA", saSeq);
        await db.SaveChangesAsync();
    }
}

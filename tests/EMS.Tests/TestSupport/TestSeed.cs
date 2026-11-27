using EMS.Domain.Common;
using EMS.Domain.MasterData;
using EMS.Domain.Shareholders;
using EMS.Domain.Workflow;
using EMS.Infrastructure.Persistence;

namespace EMS.Tests.TestSupport;

/// <summary>Minimal, fast master data for service tests - deliberately not DbSeeder (which generates a
/// demo register meant for the running app, not a per-test fixture).</summary>
public static class TestSeed
{
    public static ShareholderGroup Group(EmsDbContext db, string code = "PERSONAL")
    {
        var group = new ShareholderGroup { Code = code, NameEn = code, NameMm = code, EffectiveFrom = new DateOnly(2020, 1, 1) };
        db.ShareholderGroups.Add(group);
        db.SaveChanges();
        return group;
    }

    public static ShareClass ShareClass(EmsDbContext db, string code = "ORD")
    {
        var shareClass = new ShareClass { Code = code, NameEn = "Ordinary Shares", NameMm = "Ordinary Shares", EffectiveFrom = new DateOnly(2020, 1, 1), CarriesCertificate = true };
        db.ShareClasses.Add(shareClass);
        db.SaveChanges();
        return shareClass;
    }

    public static Shareholder ActiveShareholder(EmsDbContext db, long groupId, string shareholderNo, string name = "Test Shareholder", ShareholderStatus status = ShareholderStatus.Active, KycResult kycStatus = KycResult.Approved)
    {
        var shareholder = new Shareholder
        {
            ShareholderNo = shareholderNo,
            Type = ApplicantType.Personal,
            ShareholderGroupId = groupId,
            Status = status,
            KycStatus = kycStatus,
            RegistrationDate = new DateOnly(2024, 1, 1),
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = "system",
            Person = new Person { NameEn = name, NameMm = name, DateOfBirth = new DateOnly(1985, 1, 1), FatherName = "-", NrcNumber = "12/YAKANA(N)000000" }
        };
        db.Shareholders.Add(shareholder);
        db.SaveChanges();
        return shareholder;
    }

    /// <summary>A single-step, module-wide approval rule so WorkflowService has something to route against.</summary>
    public static ApprovalMatrixRule SingleStepRule(EmsDbContext db, string module, string approverRole, int sequence = 1)
    {
        var rule = new ApprovalMatrixRule
        {
            Module = module,
            Sequence = sequence,
            StepName = approverRole,
            ApproverRole = approverRole,
            IsMandatory = true,
            StageMode = ApprovalStageMode.Sequential,
            EffectiveFrom = new DateOnly(2020, 1, 1),
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = "system"
        };
        db.ApprovalMatrixRules.Add(rule);
        db.SaveChanges();
        return rule;
    }
}

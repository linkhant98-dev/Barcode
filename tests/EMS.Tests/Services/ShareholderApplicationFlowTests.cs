using EMS.Application.Shareholders;
using EMS.Domain.Common;
using EMS.Infrastructure.Services;
using EMS.Tests.TestSupport;
using Xunit;

namespace EMS.Tests.Services;

/// <summary>Section 6 - end to end: draft -> mandatory-data validation -> submit -> KYC decision -> registration
/// produces exactly one Shareholder (6.5), and re-registering the same application is a no-op (idempotent).</summary>
public class ShareholderApplicationFlowTests
{
    private sealed record Fixture(
        ShareholderApplicationService Applications, KycService Kyc, ShareholderRegistrationService Registration, long ShareholderGroupId);

    private static Fixture Build(SqliteTestDb db, FakeCurrentUserService? user = null)
    {
        user ??= new FakeCurrentUserService { UserId = "maker" };
        var group = TestSeed.Group(db.Context);

        var refNumbers = new ReferenceNumberService(db.Context);
        var audit = new AuditService(db.Context, user);
        var notifications = new FakeNotificationService();
        var registration = new ShareholderRegistrationService(db.Context, user, refNumbers, audit);
        var kyc = new KycService(db.Context, user, audit, registration, notifications);
        var applications = new ShareholderApplicationService(db.Context, user, refNumbers, audit, notifications);

        return new Fixture(applications, kyc, registration, group.Id);
    }

    private static async Task FillMandatoryPersonalDataAsync(SqliteTestDb db, ShareholderApplicationService service, EMS.Domain.Applications.ShareholderApplication application)
    {
        application.NameEn = "Daw Hla Hla";
        application.DateOfBirth = new DateOnly(1985, 1, 1);
        application.NrcNumber = "12/YAKANA(N)000111";
        application.Mobile = "09-111222333";
        await service.SaveDraftAsync(application);
    }

    [Fact]
    public async Task HasMandatoryDataAsync_PersonalApplicationMissingNrcNumber_ReturnsFalse()
    {
        using var db = new SqliteTestDb();
        var f = Build(db);
        var application = await f.Applications.CreateDraftAsync(new CreateApplicationRequest(ApplicantType.Personal, f.ShareholderGroupId));
        application.NameEn = "Daw Hla Hla";
        application.DateOfBirth = new DateOnly(1985, 1, 1);
        application.Mobile = "09-111222333";
        await f.Applications.SaveDraftAsync(application);

        var hasMandatoryData = await f.Applications.HasMandatoryDataAsync(application.Id);

        Assert.False(hasMandatoryData);
    }

    [Fact]
    public async Task SubmitAsync_MandatoryDataIncomplete_Throws()
    {
        using var db = new SqliteTestDb();
        var f = Build(db);
        var application = await f.Applications.CreateDraftAsync(new CreateApplicationRequest(ApplicantType.Personal, f.ShareholderGroupId));

        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Applications.SubmitAsync(application.Id));
    }

    [Fact]
    public async Task SubmitAsync_MandatoryDataComplete_MovesToKycPendingAndCreatesAKycCase()
    {
        using var db = new SqliteTestDb();
        var f = Build(db);
        var application = await f.Applications.CreateDraftAsync(new CreateApplicationRequest(ApplicantType.Personal, f.ShareholderGroupId));
        await FillMandatoryPersonalDataAsync(db, f.Applications, application);

        await f.Applications.SubmitAsync(application.Id);

        var reloaded = db.Context.ShareholderApplications.Single(a => a.Id == application.Id);
        Assert.Equal(WorkflowStatus.KycPending, reloaded.Status);
        Assert.NotNull(reloaded.SubmittedDate);
        var kycCase = Assert.Single(db.Context.KycCases);
        Assert.Equal(KycResult.Pending, kycCase.Result);
    }

    [Fact]
    public async Task FullFlow_DraftSubmitKycApprove_RegistersExactlyOneActiveShareholder()
    {
        using var db = new SqliteTestDb();
        var f = Build(db);
        var application = await f.Applications.CreateDraftAsync(new CreateApplicationRequest(ApplicantType.Personal, f.ShareholderGroupId));
        await FillMandatoryPersonalDataAsync(db, f.Applications, application);
        await f.Applications.SubmitAsync(application.Id);
        var kycCase = db.Context.KycCases.Single();

        var shareholderId = await f.Kyc.DecideAsync(new KycDecisionRequest(kycCase.Id, KycResult.Approved, RiskRating.Low, "Clean", "SCR-1"));

        var shareholder = Assert.Single(db.Context.Shareholders);
        Assert.Equal(shareholderId, shareholder.Id);
        Assert.Equal(ShareholderStatus.Active, shareholder.Status);
        Assert.Equal(KycResult.Approved, shareholder.KycStatus);
        Assert.Equal(application.Id, shareholder.SourceApplicationId);

        var reloadedApplication = db.Context.ShareholderApplications.Single(a => a.Id == application.Id);
        Assert.Equal(WorkflowStatus.Completed, reloadedApplication.Status);
        Assert.Equal(shareholderId, reloadedApplication.CreatedShareholderId);
    }

    [Fact]
    public async Task DecideAsync_Rejected_MovesApplicationToKycRejectedWithoutCreatingAShareholder()
    {
        using var db = new SqliteTestDb();
        var f = Build(db);
        var application = await f.Applications.CreateDraftAsync(new CreateApplicationRequest(ApplicantType.Personal, f.ShareholderGroupId));
        await FillMandatoryPersonalDataAsync(db, f.Applications, application);
        await f.Applications.SubmitAsync(application.Id);
        var kycCase = db.Context.KycCases.Single();

        var result = await f.Kyc.DecideAsync(new KycDecisionRequest(kycCase.Id, KycResult.Rejected, RiskRating.High, "Failed screening", null));

        Assert.Equal(0, result);
        Assert.Empty(db.Context.Shareholders);
        var reloadedApplication = db.Context.ShareholderApplications.Single(a => a.Id == application.Id);
        Assert.Equal(WorkflowStatus.KycRejected, reloadedApplication.Status);
    }

    [Fact]
    public async Task RegisterFromApplicationAsync_CalledTwiceForTheSameApplication_IsIdempotent()
    {
        using var db = new SqliteTestDb();
        var f = Build(db);
        var application = await f.Applications.CreateDraftAsync(new CreateApplicationRequest(ApplicantType.Personal, f.ShareholderGroupId));
        await FillMandatoryPersonalDataAsync(db, f.Applications, application);
        await f.Applications.SubmitAsync(application.Id);

        var firstId = await f.Registration.RegisterFromApplicationAsync(application.Id);
        var secondId = await f.Registration.RegisterFromApplicationAsync(application.Id);

        Assert.Equal(firstId, secondId);
        Assert.Single(db.Context.Shareholders);
    }
}

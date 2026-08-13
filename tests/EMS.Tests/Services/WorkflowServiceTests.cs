using EMS.Application.Workflow;
using EMS.Domain.Common;
using EMS.Infrastructure.Services;
using EMS.Tests.TestSupport;
using Xunit;

namespace EMS.Tests.Services;

/// <summary>Section 11 - the generic approval engine shared by every transaction type. These tests are the
/// highest-value coverage in the suite: maker-checker, sequential routing and mandatory-comment rules are
/// exactly the controls a bank audit would ask to see evidence of.</summary>
public class WorkflowServiceTests
{
    private static WorkflowService BuildService(SqliteTestDb db, FakeCurrentUserService user, FakeNotificationService? notifications = null) =>
        new(db.Context, user, new AuditService(db.Context, user), notifications ?? new FakeNotificationService());

    [Fact]
    public async Task SubmitForApprovalAsync_NoMatrixRuleConfigured_CreatesAnInstanceWithNoSteps()
    {
        using var db = new SqliteTestDb();
        var service = BuildService(db, new FakeCurrentUserService { UserId = "maker" });

        var instance = await service.SubmitForApprovalAsync(new SubmitForApprovalRequest("ShareTransaction", 1, "IS-2026-000001", "IS", null, null, null));

        Assert.Empty(instance.Steps);
        Assert.Equal(WorkflowStatus.PendingApproval, instance.Status);
    }

    [Fact]
    public async Task SubmitForApprovalAsync_SingleStepMatrix_CreatesOnePendingStepAndNotifiesTheApproverRole()
    {
        using var db = new SqliteTestDb();
        TestSeed.SingleStepRule(db.Context, "IS", "DGM Approver");
        var notifications = new FakeNotificationService();
        var service = BuildService(db, new FakeCurrentUserService { UserId = "maker" }, notifications);

        var instance = await service.SubmitForApprovalAsync(new SubmitForApprovalRequest("ShareTransaction", 1, "IS-2026-000001", "IS", null, null, null));

        var step = Assert.Single(instance.Steps);
        Assert.Equal(ApprovalStepStatus.Pending, step.Status);
        Assert.Equal("DGM Approver", step.ApproverRole);
        Assert.Single(notifications.RoleNotifications, n => n.Role == "DGM Approver");
    }

    [Fact]
    public async Task SubmitForApprovalAsync_MultiStepMatrix_OnlyTheFirstStepStartsPending()
    {
        using var db = new SqliteTestDb();
        TestSeed.SingleStepRule(db.Context, "IS", "DGM Approver", sequence: 1);
        TestSeed.SingleStepRule(db.Context, "IS", "Legal Approver", sequence: 2);
        var service = BuildService(db, new FakeCurrentUserService { UserId = "maker" });

        var instance = await service.SubmitForApprovalAsync(new SubmitForApprovalRequest("ShareTransaction", 1, "IS-2026-000001", "IS", null, null, null));

        Assert.Equal(2, instance.Steps.Count);
        Assert.Equal(ApprovalStepStatus.Pending, instance.Steps.Single(s => s.Sequence == 1).Status);
        Assert.Equal(ApprovalStepStatus.Waiting, instance.Steps.Single(s => s.Sequence == 2).Status);
    }

    [Fact]
    public async Task SubmitForApprovalAsync_RuleForADifferentModule_IsNotUsed()
    {
        using var db = new SqliteTestDb();
        TestSeed.SingleStepRule(db.Context, "TS", "DGM Approver");
        var service = BuildService(db, new FakeCurrentUserService { UserId = "maker" });

        var instance = await service.SubmitForApprovalAsync(new SubmitForApprovalRequest("ShareTransaction", 1, "IS-2026-000001", "IS", null, null, null));

        Assert.Empty(instance.Steps);
    }

    [Fact]
    public async Task DecideAsync_ByTheSubmitterThemselves_IsRejectedByMakerChecker()
    {
        using var db = new SqliteTestDb();
        TestSeed.SingleStepRule(db.Context, "IS", "DGM Approver");
        var maker = new FakeCurrentUserService { UserId = "maker", RoleList = ["DGM Approver"] };
        var service = BuildService(db, maker);
        var instance = await service.SubmitForApprovalAsync(new SubmitForApprovalRequest("ShareTransaction", 1, "IS-2026-000001", "IS", null, null, null));
        var stepId = instance.Steps.Single().Id;

        // Same user (the maker) tries to approve their own submission - COM-011.
        var result = await service.DecideAsync(new ApprovalDecisionRequest(stepId, ApprovalDecision.Approve, null, 0));

        Assert.False(result.Success);
        Assert.Contains("cannot approve", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DecideAsync_ByAUserWithoutTheApproverRoleOrADelegation_IsUnauthorized()
    {
        using var db = new SqliteTestDb();
        TestSeed.SingleStepRule(db.Context, "IS", "DGM Approver");
        var service = BuildService(db, new FakeCurrentUserService { UserId = "maker" });
        var instance = await service.SubmitForApprovalAsync(new SubmitForApprovalRequest("ShareTransaction", 1, "IS-2026-000001", "IS", null, null, null));
        var stepId = instance.Steps.Single().Id;

        var wrongRoleUser = new FakeCurrentUserService { UserId = "someone-else", RoleList = ["Report Viewer"] };
        var decideService = BuildService(db, wrongRoleUser);

        var result = await decideService.DecideAsync(new ApprovalDecisionRequest(stepId, ApprovalDecision.Approve, null, 0));

        Assert.False(result.Success);
        Assert.Contains("Only a DGM Approver", result.Error);
    }

    [Fact]
    public async Task DecideAsync_Reject_WithoutAComment_IsRejectedAsInvalid()
    {
        using var db = new SqliteTestDb();
        TestSeed.SingleStepRule(db.Context, "IS", "DGM Approver");
        var service = BuildService(db, new FakeCurrentUserService { UserId = "maker" });
        var instance = await service.SubmitForApprovalAsync(new SubmitForApprovalRequest("ShareTransaction", 1, "IS-2026-000001", "IS", null, null, null));
        var stepId = instance.Steps.Single().Id;

        var approver = new FakeCurrentUserService { UserId = "approver", RoleList = ["DGM Approver"] };
        var decideService = BuildService(db, approver);

        var result = await decideService.DecideAsync(new ApprovalDecisionRequest(stepId, ApprovalDecision.Reject, null, 0));

        Assert.False(result.Success);
        Assert.Contains("comment is required", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DecideAsync_ApproveTheFinalStep_CompletesTheInstanceAndNotifiesTheMaker()
    {
        using var db = new SqliteTestDb();
        TestSeed.SingleStepRule(db.Context, "IS", "DGM Approver");
        var service = BuildService(db, new FakeCurrentUserService { UserId = "maker" });
        var instance = await service.SubmitForApprovalAsync(new SubmitForApprovalRequest("ShareTransaction", 1, "IS-2026-000001", "IS", null, null, null));
        var stepId = instance.Steps.Single().Id;

        var approver = new FakeCurrentUserService { UserId = "approver", RoleList = ["DGM Approver"] };
        var notifications = new FakeNotificationService();
        var decideService = BuildService(db, approver, notifications);

        var result = await decideService.DecideAsync(new ApprovalDecisionRequest(stepId, ApprovalDecision.Approve, "Looks good", 0));

        Assert.True(result.Success);
        Assert.Equal(WorkflowStatus.Approved, result.NewStatus);
        Assert.Single(notifications.UserNotifications, n => n.UserId == "maker" && n.TemplateCode == "APPROVAL_APPROVED");
    }

    [Fact]
    public async Task DecideAsync_ApproveANonFinalStep_AdvancesToTheNextStepAndNotifiesItsRole()
    {
        using var db = new SqliteTestDb();
        TestSeed.SingleStepRule(db.Context, "IS", "DGM Approver", sequence: 1);
        TestSeed.SingleStepRule(db.Context, "IS", "Legal Approver", sequence: 2);
        var service = BuildService(db, new FakeCurrentUserService { UserId = "maker" });
        var instance = await service.SubmitForApprovalAsync(new SubmitForApprovalRequest("ShareTransaction", 1, "IS-2026-000001", "IS", null, null, null));
        var firstStepId = instance.Steps.Single(s => s.Sequence == 1).Id;

        var approver = new FakeCurrentUserService { UserId = "approver1", RoleList = ["DGM Approver"] };
        var notifications = new FakeNotificationService();
        var decideService = BuildService(db, approver, notifications);

        var result = await decideService.DecideAsync(new ApprovalDecisionRequest(firstStepId, ApprovalDecision.Approve, null, 0));

        Assert.True(result.Success);
        Assert.Equal(WorkflowStatus.PendingApproval, result.NewStatus);
        var refreshed = await decideService.GetActiveInstanceAsync("ShareTransaction", 1);
        Assert.Equal(ApprovalStepStatus.Approved, refreshed!.Steps.Single(s => s.Sequence == 1).Status);
        Assert.Equal(ApprovalStepStatus.Pending, refreshed.Steps.Single(s => s.Sequence == 2).Status);
        Assert.Single(notifications.RoleNotifications, n => n.Role == "Legal Approver");
    }

    [Fact]
    public async Task DecideAsync_Reject_StopsTheInstanceAndNotifiesTheMaker()
    {
        using var db = new SqliteTestDb();
        TestSeed.SingleStepRule(db.Context, "IS", "DGM Approver");
        var service = BuildService(db, new FakeCurrentUserService { UserId = "maker" });
        var instance = await service.SubmitForApprovalAsync(new SubmitForApprovalRequest("ShareTransaction", 1, "IS-2026-000001", "IS", null, null, null));
        var stepId = instance.Steps.Single().Id;

        var approver = new FakeCurrentUserService { UserId = "approver", RoleList = ["DGM Approver"] };
        var notifications = new FakeNotificationService();
        var decideService = BuildService(db, approver, notifications);

        var result = await decideService.DecideAsync(new ApprovalDecisionRequest(stepId, ApprovalDecision.Reject, "Missing documents", 0));

        Assert.True(result.Success);
        Assert.Equal(WorkflowStatus.Rejected, result.NewStatus);
        Assert.Single(notifications.UserNotifications, n => n.UserId == "maker" && n.TemplateCode == "APPROVAL_REJECT");
    }

    [Fact]
    public async Task DecideAsync_ByAnActiveDelegateOfTheApproverRole_IsAuthorized()
    {
        using var db = new SqliteTestDb();
        TestSeed.SingleStepRule(db.Context, "IS", "DGM Approver");
        var service = BuildService(db, new FakeCurrentUserService { UserId = "maker" });
        var instance = await service.SubmitForApprovalAsync(new SubmitForApprovalRequest("ShareTransaction", 1, "IS-2026-000001", "IS", null, null, null));
        var stepId = instance.Steps.Single().Id;

        var delegationService = new DelegationService(db.Context, new FakeCurrentUserService { UserId = "real-dgm" }, new AuditService(db.Context, new FakeCurrentUserService { UserId = "real-dgm" }));
        await delegationService.CreateAsync(new CreateDelegationRequest("stand-in", "DGM Approver", new DateOnly(2020, 1, 1), new DateOnly(2999, 12, 31), "Cover"));

        var delegate_ = new FakeCurrentUserService { UserId = "stand-in", RoleList = [] }; // no direct role - only the delegation grants access
        var decideService = BuildService(db, delegate_);

        var result = await decideService.DecideAsync(new ApprovalDecisionRequest(stepId, ApprovalDecision.Approve, null, 0));

        Assert.True(result.Success);
    }

    [Fact]
    public async Task DecideAsync_AStepAlreadyDecided_CannotBeDecidedAgain()
    {
        using var db = new SqliteTestDb();
        TestSeed.SingleStepRule(db.Context, "IS", "DGM Approver");
        var service = BuildService(db, new FakeCurrentUserService { UserId = "maker" });
        var instance = await service.SubmitForApprovalAsync(new SubmitForApprovalRequest("ShareTransaction", 1, "IS-2026-000001", "IS", null, null, null));
        var stepId = instance.Steps.Single().Id;

        var approver = new FakeCurrentUserService { UserId = "approver", RoleList = ["DGM Approver"] };
        var decideService = BuildService(db, approver);
        await decideService.DecideAsync(new ApprovalDecisionRequest(stepId, ApprovalDecision.Approve, null, 0));

        var second = await decideService.DecideAsync(new ApprovalDecisionRequest(stepId, ApprovalDecision.Approve, null, 0));

        Assert.False(second.Success);
    }
}

using EMS.Application.CorporateActions;
using EMS.Application.Shares;
using EMS.Application.Workflow;
using EMS.Domain.Common;
using EMS.Domain.CorporateActions;
using EMS.Domain.Shares;
using EMS.Domain.Workflow;
using EMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EMS.Web.Controllers;

/// <summary>Section 13 - approval inbox, review, and decision panel shared by every transaction type.</summary>
[Authorize]
public class ApprovalsController : Controller
{
    private readonly EmsDbContext _db;
    private readonly IWorkflowService _workflow;
    private readonly IIssueShareService _issueService;
    private readonly ITransferShareService _transferService;
    private readonly IBonusService _bonusService;
    private readonly IDividendService _dividendService;

    public ApprovalsController(
        EmsDbContext db, IWorkflowService workflow, IIssueShareService issueService,
        ITransferShareService transferService, IBonusService bonusService, IDividendService dividendService)
    {
        _db = db;
        _workflow = workflow;
        _issueService = issueService;
        _transferService = transferService;
        _bonusService = bonusService;
        _dividendService = dividendService;
    }

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        ViewData["Title"] = "My Pending Approvals";
        ViewData["Breadcrumb"] = new List<(string, string?)> { ("Dashboard", "/"), ("Workflow", null), ("My Pending Approvals", null) };

        var steps = await (
            from step in _db.ApprovalSteps
            join instance in _db.ApprovalInstances on step.ApprovalInstanceId equals instance.Id
            where step.Status == ApprovalStepStatus.Pending && instance.Status == WorkflowStatus.PendingApproval
            orderby instance.SubmittedAtUtc
            select step).Include(s => s.ApprovalInstance).ToListAsync(ct);

        return View(steps);
    }

    public async Task<IActionResult> SubmittedByMe(CancellationToken ct)
    {
        ViewData["Title"] = "Submitted by Me";
        ViewData["Breadcrumb"] = new List<(string, string?)> { ("Dashboard", "/"), ("Workflow", null), ("Submitted by Me", null) };

        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "";
        var instances = await _db.ApprovalInstances.Include(i => i.Steps)
            .Where(i => i.SubmittedByUserId == userId)
            .OrderByDescending(i => i.Id)
            .ToListAsync(ct);

        return View(instances);
    }

    public async Task<IActionResult> Review(long id, CancellationToken ct)
    {
        var step = await _db.ApprovalSteps.Include(s => s.ApprovalInstance)!.ThenInclude(i => i!.Steps)
            .FirstOrDefaultAsync(s => s.Id == id, ct);
        if (step?.ApprovalInstance is null) return NotFound();

        ViewData["Title"] = $"Review - {step.ApprovalInstance.EntityReference}";
        ViewData["Breadcrumb"] = new List<(string, string?)> { ("Dashboard", "/"), ("My Pending Approvals", Url.Action("Index")), (step.ApprovalInstance.EntityReference, null) };

        return View(step);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Decide(long approvalStepId, ApprovalDecision decision, string? comment, CancellationToken ct)
    {
        var step = await _db.ApprovalSteps.Include(s => s.ApprovalInstance).FirstOrDefaultAsync(s => s.Id == approvalStepId, ct);
        if (step?.ApprovalInstance is null) return NotFound();

        var instance = step.ApprovalInstance;
        var result = await _workflow.DecideAsync(new ApprovalDecisionRequest(approvalStepId, decision, comment, 0), ct);

        if (!result.Success)
        {
            TempData["Error"] = result.Error;
            return RedirectToAction(nameof(Review), new { id = approvalStepId });
        }

        if (result.NewStatus == WorkflowStatus.Approved)
            await PostApprovedEntityAsync(instance, ct);

        TempData["Success"] = $"Decision recorded: {decision}.";
        return RedirectToAction(nameof(Index));
    }

    /// <summary>Dispatches to the module-specific posting service once the full approval chain completes.</summary>
    private async Task PostApprovedEntityAsync(ApprovalInstance instance, CancellationToken ct)
    {
        switch (instance.EntityType)
        {
            case nameof(ShareTransaction):
                var transaction = await _db.ShareTransactions.FindAsync([instance.EntityId], ct);
                if (transaction?.Type == ShareTransactionType.IssueShares)
                    await _issueService.PostApprovedAsync(instance.EntityId, ct);
                else if (transaction?.Type == ShareTransactionType.TransferShares)
                    await _transferService.PostApprovedAsync(instance.EntityId, ct);
                break;

            case nameof(BonusEvent):
                await _bonusService.PostApprovedAsync(instance.EntityId, ct);
                break;

            case nameof(DividendEvent):
                await _dividendService.PostApprovedAsync(instance.EntityId, ct);
                break;
        }
    }
}

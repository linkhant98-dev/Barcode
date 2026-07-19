using EMS.Application.Abstractions;
using EMS.Application.Shareholders;
using EMS.Domain.Common;
using EMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EMS.Infrastructure.Services;

/// <summary>Section 6.4 - KYC officer decisions. Approval triggers shareholder registration (6.1 step 7).</summary>
public class KycService : IKycService
{
    private readonly EmsDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditService _audit;
    private readonly IShareholderRegistrationService _registration;
    private readonly INotificationService _notifications;

    public KycService(
        EmsDbContext db, ICurrentUserService currentUser, IAuditService audit,
        IShareholderRegistrationService registration, INotificationService notifications)
    {
        _db = db;
        _currentUser = currentUser;
        _audit = audit;
        _registration = registration;
        _notifications = notifications;
    }

    public async Task<long> DecideAsync(KycDecisionRequest request, CancellationToken ct = default)
    {
        var kycCase = await _db.KycCases
            .Include(k => k.ShareholderApplication)
            .FirstOrDefaultAsync(k => k.Id == request.KycCaseId, ct)
            ?? throw new InvalidOperationException("KYC case not found.");

        var application = kycCase.ShareholderApplication
            ?? throw new InvalidOperationException("Application not found for KYC case.");

        kycCase.Result = request.Result;
        kycCase.RiskRating = request.RiskRating;
        kycCase.ScreeningReference = request.ScreeningReference;
        kycCase.ScreeningTimestampUtc = DateTime.UtcNow;
        kycCase.DecisionUserId = _currentUser.UserId;
        kycCase.DecisionAtUtc = DateTime.UtcNow;
        kycCase.DecisionRemark = request.Remark;
        kycCase.ModifiedAtUtc = DateTime.UtcNow;
        kycCase.ModifiedBy = _currentUser.UserId;

        switch (request.Result)
        {
            case KycResult.Approved:
                application.Status = WorkflowStatus.KycApproved;
                break;
            case KycResult.Rejected:
                application.Status = WorkflowStatus.KycRejected;
                break;
            case KycResult.Reverted:
            case KycResult.PendingAdditionalInformation:
                application.Status = WorkflowStatus.Reverted;
                break;
        }

        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("Decision", "SA", "KycCase", application.ApplicationNo,
            after: new { request.Result, request.Remark }, ct: ct);

        await _notifications.NotifyUserAsync(application.MakerUserId, "KYC_DECIDED",
            $"KYC {request.Result} - {application.ApplicationNo}", $"KYC decision recorded: {request.Result}.",
            application.ApplicationNo, $"/ShareholderApplications/Edit/{application.Id}", ct);

        if (request.Result == KycResult.Approved)
        {
            var shareholderId = await _registration.RegisterFromApplicationAsync(application.Id, ct);
            return shareholderId;
        }

        return 0;
    }
}

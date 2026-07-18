using EMS.Application.Abstractions;
using EMS.Application.Shareholders;
using EMS.Application.Workflow;
using EMS.Domain.Applications;
using EMS.Domain.Common;
using EMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EMS.Infrastructure.Services;

/// <summary>Section 6.1 - create, save-as-draft (COM-012) and submit a Shareholder Application to the KYC queue.</summary>
public class ShareholderApplicationService : IShareholderApplicationService
{
    private readonly EmsDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IReferenceNumberService _refNumbers;
    private readonly IAuditService _audit;

    public ShareholderApplicationService(
        EmsDbContext db, ICurrentUserService currentUser, IReferenceNumberService refNumbers, IAuditService audit)
    {
        _db = db;
        _currentUser = currentUser;
        _refNumbers = refNumbers;
        _audit = audit;
    }

    public async Task<ShareholderApplication> CreateDraftAsync(CreateApplicationRequest request, CancellationToken ct = default)
    {
        var application = new ShareholderApplication
        {
            ApplicationNo = await _refNumbers.NextAsync("SA", ct: ct),
            Type = request.Type,
            ShareholderGroupId = request.ShareholderGroupId,
            Status = WorkflowStatus.Draft,
            MakerUserId = _currentUser.UserId,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = _currentUser.UserId
        };

        _db.ShareholderApplications.Add(application);
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("Create", "SA", nameof(ShareholderApplication), application.ApplicationNo, ct: ct);
        return application;
    }

    public async Task SaveDraftAsync(ShareholderApplication application, CancellationToken ct = default)
    {
        application.ModifiedAtUtc = DateTime.UtcNow;
        application.ModifiedBy = _currentUser.UserId;
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("Edit", "SA", nameof(ShareholderApplication), application.ApplicationNo, ct: ct);
    }

    /// <summary>6.5 acceptance: all mandatory data and documents are validated before submission.</summary>
    public async Task<bool> HasMandatoryDataAsync(long applicationId, CancellationToken ct = default)
    {
        var application = await _db.ShareholderApplications.FindAsync([applicationId], ct);
        if (application is null) return false;

        return application.Type switch
        {
            ApplicantType.Personal => !string.IsNullOrWhiteSpace(application.NameEn)
                && application.DateOfBirth is not null
                && !string.IsNullOrWhiteSpace(application.NrcNumber)
                && !string.IsNullOrWhiteSpace(application.Mobile),
            ApplicantType.Corporate => !string.IsNullOrWhiteSpace(application.LegalNameEn)
                && !string.IsNullOrWhiteSpace(application.RegistrationNumber)
                && application.CorporateRegistrationDate is not null,
            ApplicantType.Joint => !string.IsNullOrWhiteSpace(application.NameEn)
                && await _db.ApplicationJointHolders.AnyAsync(j => j.ShareholderApplicationId == applicationId, ct),
            _ => false
        };
    }

    public async Task SubmitAsync(long applicationId, CancellationToken ct = default)
    {
        if (!await HasMandatoryDataAsync(applicationId, ct))
            throw new InvalidOperationException("Mandatory application data is incomplete.");

        var application = await _db.ShareholderApplications.FindAsync([applicationId], ct)
            ?? throw new InvalidOperationException("Application not found.");

        application.Status = WorkflowStatus.KycPending;
        application.SubmittedDate = DateOnly.FromDateTime(DateTime.UtcNow);

        application.KycCase = new KycCase
        {
            RequestedAtUtc = DateTime.UtcNow,
            RequestedByUserId = _currentUser.UserId,
            Result = KycResult.Pending,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = _currentUser.UserId
        };

        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("Submit", "SA", nameof(ShareholderApplication), application.ApplicationNo, ct: ct);
    }
}

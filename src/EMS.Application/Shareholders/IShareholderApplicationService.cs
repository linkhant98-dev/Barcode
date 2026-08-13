using EMS.Domain.Applications;
using EMS.Domain.Common;

namespace EMS.Application.Shareholders;

public record CreateApplicationRequest(ApplicantType Type, long ShareholderGroupId);

/// <summary>Section 6 - Shareholder Application create/submit workflow (draft -> submit -> KYC queue).</summary>
public interface IShareholderApplicationService
{
    Task<ShareholderApplication> CreateDraftAsync(CreateApplicationRequest request, CancellationToken ct = default);
    Task SaveDraftAsync(ShareholderApplication application, CancellationToken ct = default);
    Task<bool> HasMandatoryDataAsync(long applicationId, CancellationToken ct = default);
    Task SubmitAsync(long applicationId, CancellationToken ct = default);
}

public record KycDecisionRequest(long KycCaseId, KycResult Result, RiskRating RiskRating, string? Remark, string? ScreeningReference);

/// <summary>Section 6.4 - KYC work queue decisions. Approval creates the shareholder master (6.1 step 7).</summary>
public interface IKycService
{
    Task<long> DecideAsync(KycDecisionRequest request, CancellationToken ct = default);
}

/// <summary>Section 6.1 / 6.5 - registers exactly one Shareholder + one ShareholderNo from an approved application.</summary>
public interface IShareholderRegistrationService
{
    Task<long> RegisterFromApplicationAsync(long applicationId, CancellationToken ct = default);
}

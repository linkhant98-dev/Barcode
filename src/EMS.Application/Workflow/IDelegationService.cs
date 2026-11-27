namespace EMS.Application.Workflow;

public record CreateDelegationRequest(string ToUserId, string ApproverRole, DateOnly StartDate, DateOnly EndDate, string Reason);

/// <summary>Section 11.1 - temporary delegation of an approver role with dates, reason, and no self-delegation.</summary>
public interface IDelegationService
{
    Task<long> CreateAsync(CreateDelegationRequest request, CancellationToken ct = default);
    Task EndAsync(long delegationId, CancellationToken ct = default);
}

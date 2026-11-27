using EMS.Application.Abstractions;
using EMS.Application.Workflow;
using EMS.Domain.Workflow;
using EMS.Infrastructure.Persistence;

namespace EMS.Infrastructure.Services;

/// <summary>Section 11.1 - temporary delegation with dates and reason; no self-delegation.</summary>
public class DelegationService : IDelegationService
{
    private readonly EmsDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditService _audit;

    public DelegationService(EmsDbContext db, ICurrentUserService currentUser, IAuditService audit)
    {
        _db = db;
        _currentUser = currentUser;
        _audit = audit;
    }

    public async Task<long> CreateAsync(CreateDelegationRequest request, CancellationToken ct = default)
    {
        if (request.ToUserId == _currentUser.UserId)
            throw new InvalidOperationException("You cannot delegate approval authority to yourself.");

        if (request.EndDate < request.StartDate)
            throw new InvalidOperationException("End date cannot be before the start date.");

        var delegation = new Delegation
        {
            FromUserId = _currentUser.UserId,
            ToUserId = request.ToUserId,
            ApproverRole = request.ApproverRole,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            Reason = request.Reason,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = _currentUser.UserId
        };

        _db.Delegations.Add(delegation);
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("Create", "Delegation", nameof(Delegation), delegation.Id.ToString(), ct: ct);
        return delegation.Id;
    }

    public async Task EndAsync(long delegationId, CancellationToken ct = default)
    {
        var delegation = await _db.Delegations.FindAsync([delegationId], ct)
            ?? throw new InvalidOperationException("Delegation not found.");

        delegation.IsActive = false;
        delegation.ModifiedAtUtc = DateTime.UtcNow;
        delegation.ModifiedBy = _currentUser.UserId;
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("Edit", "Delegation", nameof(Delegation), delegation.Id.ToString(), after: new { Ended = true }, ct: ct);
    }
}

using EMS.Application.Abstractions;
using EMS.Application.Shareholders;
using EMS.Domain.Common;
using EMS.Domain.Shareholders;
using EMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EMS.Infrastructure.Services;

/// <summary>6.5 acceptance: a valid approved application creates exactly one Shareholder with one unique ShareholderNo.</summary>
public class ShareholderRegistrationService : IShareholderRegistrationService
{
    private readonly EmsDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IReferenceNumberService _refNumbers;
    private readonly IAuditService _audit;

    public ShareholderRegistrationService(
        EmsDbContext db, ICurrentUserService currentUser, IReferenceNumberService refNumbers, IAuditService audit)
    {
        _db = db;
        _currentUser = currentUser;
        _refNumbers = refNumbers;
        _audit = audit;
    }

    public async Task<long> RegisterFromApplicationAsync(long applicationId, CancellationToken ct = default)
    {
        var application = await _db.ShareholderApplications
            .Include(a => a.JointHolders)
            .Include(a => a.Directors)
            .Include(a => a.AuthorizedSigners)
            .Include(a => a.BeneficialOwners)
            .FirstOrDefaultAsync(a => a.Id == applicationId, ct)
            ?? throw new InvalidOperationException("Application not found.");

        if (application.CreatedShareholderId is not null)
            return application.CreatedShareholderId.Value; // idempotent - already registered

        var shareholder = new Shareholder
        {
            // 4.1.1 - a Temporary Shareholder ID is issued right after KYC approval; RegisterFromApplicationAsync
            // is called from that same decision (KycService.DecideAsync), so IsPermanentId stays false until
            // PromoteToPermanentIdAsync is called once Board/CBM approval is filed.
            ShareholderNo = await _refNumbers.NextTemporaryShareholderNoAsync(ct),
            IsPermanentId = false,
            Type = application.Type,
            ShareholderGroupId = application.ShareholderGroupId,
            Status = ShareholderStatus.Active,
            RegistrationDate = DateOnly.FromDateTime(DateTime.UtcNow),
            KycStatus = KycResult.Approved,
            KycApprovalDate = DateOnly.FromDateTime(DateTime.UtcNow),
            SourceApplicationId = application.Id,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = _currentUser.UserId
        };

        if (application.Type == ApplicantType.Corporate)
        {
            var corporate = new Corporate
            {
                LegalNameEn = application.LegalNameEn ?? string.Empty,
                RegistrationNumber = application.RegistrationNumber ?? string.Empty,
                RegistrationDate = application.CorporateRegistrationDate ?? DateOnly.FromDateTime(DateTime.UtcNow),
                LegalForm = application.LegalForm ?? string.Empty,
                TaxIdentifier = application.TaxIdentifier,
                ContactPersonName = application.NameEn ?? string.Empty,
                DateOfIncorporation = application.DateOfIncorporation,
                TypeOfInstitution = application.TypeOfInstitution,
                NatureOfBusiness = application.NatureOfBusiness,
                SourceOfFund = application.SourceOfFund,
                PaidUpCapital = application.PaidUpCapital,
                BoardResolutionDate = application.BoardResolutionDate
            };

            foreach (var director in application.Directors)
            {
                corporate.Signatories.Add(new CorporateSignatory
                {
                    Name = director.Name,
                    NrcNumber = director.NrcNumber,
                    ResidentialAddress = director.ResidentialAddress,
                    IsDirector = true
                });
            }
            foreach (var signer in application.AuthorizedSigners)
            {
                corporate.Signatories.Add(new CorporateSignatory
                {
                    Name = signer.Name,
                    NrcNumber = signer.NrcNumber,
                    Position = signer.Designation,
                    IsAuthorizedSigner = true
                });
            }
            foreach (var owner in application.BeneficialOwners)
            {
                corporate.BeneficialOwners.Add(new BeneficialOwner
                {
                    Name = owner.OwnerName,
                    NrcOrRegistrationNumber = owner.NrcNumber,
                    OwnershipPercentage = owner.OwnershipPercentage
                });
            }

            shareholder.Corporate = corporate;
        }
        else
        {
            shareholder.Person = new Person
            {
                NameEn = application.NameEn ?? string.Empty,
                NameMm = application.NameMm ?? string.Empty,
                DateOfBirth = application.DateOfBirth ?? default,
                FatherName = application.FatherName ?? string.Empty,
                NrcPrefixCode = application.NrcPrefixCode ?? string.Empty,
                NrcNumber = application.NrcNumber ?? string.Empty
            };
        }

        if (!string.IsNullOrWhiteSpace(application.AddressLine1))
        {
            shareholder.Addresses.Add(new Address
            {
                AddressType = Domain.Common.AddressType.Current,
                Line1 = application.AddressLine1,
                Township = application.Township ?? string.Empty,
                City = application.City ?? string.Empty,
                StateRegion = application.StateRegion ?? string.Empty
            });
        }

        if (!string.IsNullOrWhiteSpace(application.Mobile))
            shareholder.Contacts.Add(new Contact { ContactType = ContactType.Mobile, Value = application.Mobile, IsPrimary = true });
        if (!string.IsNullOrWhiteSpace(application.Email))
            shareholder.Contacts.Add(new Contact { ContactType = ContactType.Email, Value = application.Email });

        foreach (var joint in application.JointHolders)
        {
            shareholder.JointHolders.Add(new JointHolder
            {
                NameEn = joint.NameEn,
                NrcNumber = joint.NrcNumber,
                OwnershipPercentage = joint.OwnershipPercentage,
                IsPrimaryContact = joint.IsPrimaryContact
            });
        }

        _db.Shareholders.Add(shareholder);
        await _db.SaveChangesAsync(ct); // assigns shareholder.Id via the identity column

        application.CreatedShareholderId = shareholder.Id;
        application.Status = WorkflowStatus.Completed;
        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync("Create", "SA", nameof(Shareholder), shareholder.ShareholderNo, ct: ct);

        return shareholder.Id;
    }

    public async Task PromoteToPermanentIdAsync(long shareholderId, CancellationToken ct = default)
    {
        var shareholder = await _db.Shareholders.FindAsync([shareholderId], ct)
            ?? throw new InvalidOperationException("Shareholder not found.");

        if (shareholder.IsPermanentId)
            return; // idempotent - already promoted

        var oldNo = shareholder.ShareholderNo;
        if (shareholder.ShareholderNo.StartsWith("TSH", StringComparison.Ordinal))
            shareholder.ShareholderNo = "SH-" + shareholder.ShareholderNo[3..];

        shareholder.IsPermanentId = true;
        shareholder.PermanentIdDate = DateOnly.FromDateTime(DateTime.UtcNow);
        shareholder.ModifiedAtUtc = DateTime.UtcNow;
        shareholder.ModifiedBy = _currentUser.UserId;

        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("Update", "SH", nameof(Shareholder), shareholder.ShareholderNo,
            before: new { ShareholderNo = oldNo }, after: new { shareholder.ShareholderNo }, ct: ct);
    }
}

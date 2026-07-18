using System.ComponentModel.DataAnnotations;
using EMS.Domain.Common;

namespace EMS.Web.Models;

public class CreateApplicationViewModel
{
    [Required]
    [Display(Name = "Applicant type")]
    public ApplicantType Type { get; set; }

    [Required]
    [Display(Name = "Shareholder group")]
    public long ShareholderGroupId { get; set; }
}

/// <summary>Backs the single-page application form (10.3 form section pattern). Fields shown depend on Type.</summary>
public class EditApplicationViewModel
{
    public long Id { get; set; }
    public string ApplicationNo { get; set; } = string.Empty;
    public ApplicantType Type { get; set; }
    public string Status { get; set; } = string.Empty;

    [Display(Name = "Shareholder group")]
    public long ShareholderGroupId { get; set; }

    // Personal
    [Display(Name = "Name (English)")]
    public string? NameEn { get; set; }
    [Display(Name = "Name (Myanmar)")]
    public string? NameMm { get; set; }
    [Display(Name = "Date of birth")]
    [DataType(DataType.Date)]
    public DateOnly? DateOfBirth { get; set; }
    [Display(Name = "Father's name")]
    public string? FatherName { get; set; }
    [Display(Name = "NRC prefix")]
    public string? NrcPrefixCode { get; set; }
    [Display(Name = "NRC number")]
    public string? NrcNumber { get; set; }

    // Corporate
    [Display(Name = "Legal name")]
    public string? LegalNameEn { get; set; }
    [Display(Name = "Registration number")]
    public string? RegistrationNumber { get; set; }
    [Display(Name = "Registration date")]
    [DataType(DataType.Date)]
    public DateOnly? CorporateRegistrationDate { get; set; }
    [Display(Name = "Legal form")]
    public string? LegalForm { get; set; }
    [Display(Name = "Tax identifier")]
    public string? TaxIdentifier { get; set; }

    // Address / contact
    [Display(Name = "Address line 1")]
    public string? AddressLine1 { get; set; }
    public string? Township { get; set; }
    public string? City { get; set; }
    [Display(Name = "State / Region")]
    public string? StateRegion { get; set; }
    public string? Mobile { get; set; }
    public string? Email { get; set; }

    public List<ApplicationJointHolderRow> JointHolders { get; set; } = new();
}

public class ApplicationJointHolderRow
{
    public string NameEn { get; set; } = string.Empty;
    public string NrcNumber { get; set; } = string.Empty;
    public decimal OwnershipPercentage { get; set; }
}

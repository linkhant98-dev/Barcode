using System.ComponentModel.DataAnnotations;
using EMS.Domain.Common;

namespace EMS.Web.Models;

public class CreateUserViewModel
{
    [Required, Display(Name = "Full name")]
    public string FullName { get; set; } = string.Empty;

    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required, DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Required, Display(Name = "Role")]
    public string Role { get; set; } = string.Empty;
}

public class CreateApprovalMatrixRuleViewModel
{
    [Required]
    public string Module { get; set; } = "SA";

    [Required, Display(Name = "Sequence")]
    public int Sequence { get; set; } = 1;

    [Required, Display(Name = "Step name")]
    public string StepName { get; set; } = string.Empty;

    [Required, Display(Name = "Approver role")]
    public string ApproverRole { get; set; } = string.Empty;

    [Display(Name = "Mandatory")]
    public bool IsMandatory { get; set; } = true;

    [Display(Name = "Effective from")]
    [DataType(DataType.Date)]
    public DateOnly EffectiveFrom { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);
}

public class MasterDataItemViewModel
{
    public string Type { get; set; } = string.Empty;
    [Required]
    public string Code { get; set; } = string.Empty;
    [Required, Display(Name = "Name (English)")]
    public string NameEn { get; set; } = string.Empty;
    [Display(Name = "Name (Myanmar)")]
    public string NameMm { get; set; } = string.Empty;
}

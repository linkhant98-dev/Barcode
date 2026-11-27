using System.ComponentModel.DataAnnotations;

namespace EMS.Web.Models;

public class CreateDelegationViewModel
{
    [Required, Display(Name = "Delegate to")]
    public string ToUserId { get; set; } = string.Empty;

    [Required, Display(Name = "Approver role")]
    public string ApproverRole { get; set; } = string.Empty;

    [Required, Display(Name = "Start date")]
    [DataType(DataType.Date)]
    public DateOnly StartDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);

    [Required, Display(Name = "End date")]
    [DataType(DataType.Date)]
    public DateOnly EndDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7));

    [Required]
    public string Reason { get; set; } = string.Empty;
}

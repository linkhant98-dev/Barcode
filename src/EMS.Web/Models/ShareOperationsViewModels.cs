using System.ComponentModel.DataAnnotations;
using EMS.Domain.Common;

namespace EMS.Web.Models;

public class CreateIssueViewModel
{
    [Required]
    public IssueApplyType ApplyType { get; set; } = IssueApplyType.IssueShare;

    [Required]
    [Display(Name = "Shareholder")]
    public long ShareholderId { get; set; }

    [Required]
    [Display(Name = "Share class")]
    public long ShareClassId { get; set; }

    [Required]
    [Display(Name = "Issue date")]
    [DataType(DataType.Date)]
    public DateOnly IssueDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);

    [Required]
    [Range(1, double.MaxValue, ErrorMessage = "Number of shares must be greater than zero.")]
    [Display(Name = "Number of shares")]
    public decimal NumberOfShares { get; set; }

    [Required]
    [Display(Name = "Capital value per share")]
    public decimal CapitalValuePerShare { get; set; }

    [Display(Name = "Premium value per share")]
    public decimal PremiumValuePerShare { get; set; }

    [Display(Name = "Cash amount")]
    public decimal CashAmount { get; set; }

    [Display(Name = "Cheque amount")]
    public decimal ChequeAmount { get; set; }

    [Display(Name = "Cheque number")]
    public string? ChequeNumber { get; set; }

    [Display(Name = "Cheque date")]
    [DataType(DataType.Date)]
    public DateOnly? ChequeDate { get; set; }
}

public class CreateTransferViewModel
{
    [Required]
    [Display(Name = "Transfer from (shareholder)")]
    public long FromShareholderId { get; set; }

    [Required]
    [Display(Name = "Transfer to (shareholder)")]
    public long ToShareholderId { get; set; }

    [Required]
    [Display(Name = "Share class")]
    public long ShareClassId { get; set; }

    [Required]
    [Display(Name = "Transfer date")]
    [DataType(DataType.Date)]
    public DateOnly TransferDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);

    [Required]
    [Range(1, double.MaxValue, ErrorMessage = "Quantity must be greater than zero.")]
    public decimal Quantity { get; set; }

    [Required]
    [Display(Name = "Transfer type")]
    public TransferType TransferType { get; set; } = TransferType.Trade;

    [Display(Name = "Capital amount")]
    public decimal CapitalAmount { get; set; }
    [Display(Name = "Premium amount")]
    public decimal PremiumAmount { get; set; }
    [Display(Name = "Cash amount")]
    public decimal CashAmount { get; set; }
    [Display(Name = "Cheque amount")]
    public decimal ChequeAmount { get; set; }

    [Display(Name = "Non-trade reason")]
    public NonTradeReason? NonTradeReason { get; set; }
    [Display(Name = "Reason details")]
    public string? NonTradeReasonDetail { get; set; }
}

public class CreateBonusEventViewModel
{
    [Required]
    [Display(Name = "Financial year")]
    public string FinancialYear { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Record date")]
    [DataType(DataType.Date)]
    public DateOnly RecordDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);

    [Required]
    [Display(Name = "Bonus ratio - shares per")]
    public int BonusNumerator { get; set; } = 1;

    [Required]
    [Display(Name = "Bonus ratio - for every")]
    public int BonusDenominator { get; set; } = 6;

    [Display(Name = "Cash bonus rate per remainder share")]
    public decimal CashBonusRatePerRemainderShare { get; set; } = 5000m;

    [Required]
    [Display(Name = "Share class")]
    public long ShareClassId { get; set; }
}

public class CreateDividendEventViewModel
{
    [Required]
    [Display(Name = "Financial year")]
    public string FinancialYear { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Record date")]
    [DataType(DataType.Date)]
    public DateOnly RecordDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);

    [Required]
    [Display(Name = "Dividend percentage")]
    public decimal DividendPercentage { get; set; } = 8m;

    [Required]
    [Display(Name = "Capital value per share")]
    public decimal CapitalValuePerShare { get; set; } = 10000m;
}

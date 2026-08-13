using EMS.Domain.Shareholders;
using EMS.Domain.Shares;

namespace EMS.Web.Models;

public class ShareholderProfileViewModel
{
    public Shareholder Shareholder { get; set; } = null!;
    public decimal TotalShares { get; set; }
    public decimal PaidUpCapital { get; set; }
    public List<ShareLedgerEntry> LedgerEntries { get; set; } = new();
    public List<ShareCertificate> Certificates { get; set; } = new();
}

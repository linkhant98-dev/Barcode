using EMS.Domain.Common;

namespace EMS.Web.Models;

/// <summary>
/// Backs the public, unauthenticated certificate verification page reached by scanning a certificate's QR code.
/// Deliberately scoped to authenticity fields only - NRC number, address and contact details are never
/// populated here, since this page is reachable by anyone who can see a printed certificate, not just its
/// rightful holder. See <see cref="EMS.Web.Controllers.VerifyController"/>.
/// </summary>
public class VerifyCertificateViewModel
{
    public string CertificateNumber { get; set; } = string.Empty;
    public bool Found { get; set; }
    public string? HolderName { get; set; }
    public string? ShareClassName { get; set; }
    public decimal Quantity { get; set; }
    public DateOnly IssueDate { get; set; }
    public CertificateStatus Status { get; set; }

    /// <summary>True for statuses that mean "this is a genuine, currently-valid certificate" (Active/Printed/
    /// Delivered/Uncollected). False for Cancelled/Replaced, which still confirm the certificate once existed
    /// but flag that it is no longer the current valid one.</summary>
    public bool IsCurrentlyValid => Status is CertificateStatus.Active or CertificateStatus.Printed
        or CertificateStatus.Delivered or CertificateStatus.Uncollected;
}

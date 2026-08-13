using EMS.Infrastructure.Persistence;
using EMS.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EMS.Web.Controllers;

/// <summary>
/// Public, unauthenticated certificate verification - the target of the QR code printed on every share
/// certificate (see <see cref="CertificatesController.DownloadPdf"/>). Deliberately has no [Authorize]: anyone
/// with the printed certificate (or its QR) can confirm it is genuine without signing in.
///
/// Scope is intentionally narrow. This controller must never return NRC numbers, addresses, phone/email, or
/// any other shareholder PII/account data - only the fields needed to answer "is this certificate genuine and
/// current" (status, holder name, share class, quantity, issue date). Full shareholder detail stays behind
/// <see cref="ShareholdersController"/> and <see cref="CertificatesController"/>, both of which require sign-in.
/// </summary>
public class VerifyController : Controller
{
    private readonly EmsDbContext _db;

    public VerifyController(EmsDbContext db) => _db = db;

    [HttpGet("/verify/{certificateNumber}")]
    public async Task<IActionResult> Details(string certificateNumber, CancellationToken ct)
    {
        ViewData["Title"] = "Certificate Verification";

        var certificate = await _db.ShareCertificates
            .Include(c => c.Shareholder)!.ThenInclude(s => s!.Person)
            .Include(c => c.Shareholder)!.ThenInclude(s => s!.Corporate)
            .Include(c => c.ShareClass)
            .FirstOrDefaultAsync(c => c.CertificateNumber == certificateNumber, ct);

        if (certificate is null)
            return View(new VerifyCertificateViewModel { CertificateNumber = certificateNumber, Found = false });

        var shareholder = certificate.Shareholder;
        var holderName = shareholder?.Type == Domain.Common.ApplicantType.Corporate
            ? shareholder.Corporate?.LegalNameEn
            : shareholder?.Person?.NameEn;

        var model = new VerifyCertificateViewModel
        {
            CertificateNumber = certificate.CertificateNumber,
            Found = true,
            HolderName = holderName ?? "—",
            ShareClassName = certificate.ShareClass?.NameEn,
            Quantity = certificate.Quantity,
            IssueDate = certificate.IssueDate,
            Status = certificate.Status
        };
        return View(model);
    }
}

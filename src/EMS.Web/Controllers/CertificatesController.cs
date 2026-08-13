using EMS.Infrastructure.Persistence;
using EMS.Web.Models;
using EMS.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using ZXing;
using ZXing.Common;
using ZXing.OneD;
using ZXing.QrCode;

namespace EMS.Web.Controllers;

/// <summary>Section 5.3 (nav) / 7.4 (Certificate outputs) - share certificate register and printable document.</summary>
[Authorize]
public class CertificatesController : Controller
{
    private readonly EmsDbContext _db;

    public CertificatesController(EmsDbContext db) => _db = db;

    public async Task<IActionResult> Index(string? status, CancellationToken ct)
    {
        ViewData["Title"] = "Certificates";
        ViewData["Breadcrumb"] = new List<(string, string?)> { ("Dashboard", "/"), ("Certificates", null) };

        var query = _db.ShareCertificates.Include(c => c.Shareholder).Include(c => c.ShareClass).AsQueryable();
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<Domain.Common.CertificateStatus>(status, out var parsed))
            query = query.Where(c => c.Status == parsed);

        ViewBag.StatusFilter = status;
        var certificates = await query.OrderByDescending(c => c.IssueDate).Take(200).ToListAsync(ct);
        return View(certificates);
    }

    /// <summary>Exports the full matching certificate register (not just the 200-row on-screen preview) as CSV.</summary>
    public async Task<IActionResult> ExportCsv(string? status, CancellationToken ct)
    {
        var query = _db.ShareCertificates.Include(c => c.Shareholder)!.ThenInclude(s => s!.Person)
            .Include(c => c.Shareholder)!.ThenInclude(s => s!.Corporate)
            .Include(c => c.ShareClass).AsQueryable();
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<Domain.Common.CertificateStatus>(status, out var parsed))
            query = query.Where(c => c.Status == parsed);

        var certificates = await query.OrderByDescending(c => c.IssueDate).ToListAsync(ct);
        var headers = new[] { "Certificate No.", "Holder", "Class", "Quantity", "Issue Date", "Status" };
        var rows = certificates.Select(c => (IReadOnlyList<object?>)new object?[]
        {
            c.CertificateNumber,
            c.Shareholder?.Type == Domain.Common.ApplicantType.Corporate ? c.Shareholder.Corporate?.LegalNameEn : c.Shareholder?.Person?.NameEn,
            c.ShareClass?.NameEn, c.Quantity, c.IssueDate.ToString("dd MMM yyyy"), c.Status.ToString()
        });

        var bytes = CsvExportHelper.Build(headers, rows);
        return File(bytes, "text/csv", $"Certificates-{DateTime.UtcNow:yyyyMMdd-HHmmss}.csv");
    }

    public async Task<IActionResult> Details(long id, CancellationToken ct)
    {
        var certificate = await _db.ShareCertificates
            .Include(c => c.Shareholder)!.ThenInclude(s => s!.Person)
            .Include(c => c.Shareholder)!.ThenInclude(s => s!.Corporate)
            .Include(c => c.ShareClass)
            .FirstOrDefaultAsync(c => c.Id == id, ct);
        if (certificate is null) return NotFound();

        ViewData["Title"] = certificate.CertificateNumber;
        ViewData["Breadcrumb"] = new List<(string, string?)> { ("Dashboard", "/"), ("Certificates", Url.Action("Index")), (certificate.CertificateNumber, null) };
        return View(certificate);
    }

    /// <summary>
    /// Section 24 (print theme) - branded certificate PDF with a Code128 barcode and a QR verification code.
    /// Two-panel layout (counterfoil stub + main certificate) matching the format learned from CB Bank's real
    /// share certificate. Field labels are English-only: this deployment has no Myanmar-capable font bundled
    /// (Lato, QuestPDF's default, does not cover the Myanmar Unicode block), so bilingual glyphs would render
    /// as missing/blank rather than text. Add a font such as Noto Sans Myanmar under wwwroot/fonts and register
    /// it with QuestPDF.Drawing.FontManager.RegisterFont to light up bilingual text here.
    /// </summary>
    public async Task<IActionResult> DownloadPdf(long id, CancellationToken ct)
    {
        var certificate = await _db.ShareCertificates
            .Include(c => c.Shareholder)!.ThenInclude(s => s!.Person)
            .Include(c => c.Shareholder)!.ThenInclude(s => s!.Corporate)
            .Include(c => c.Shareholder)!.ThenInclude(s => s!.Addresses)
            .Include(c => c.ShareClass)
            .FirstOrDefaultAsync(c => c.Id == id, ct);
        if (certificate is null) return NotFound();

        var shareholder = certificate.Shareholder!;
        var holderName = shareholder.Type == Domain.Common.ApplicantType.Corporate
            ? shareholder.Corporate?.LegalNameEn
            : shareholder.Person?.NameEn;
        var nrcOrReg = shareholder.Person?.NrcNumber ?? shareholder.Corporate?.RegistrationNumber ?? "—";
        var address = FormatAddress(shareholder.Addresses.FirstOrDefault());
        var serialRange = certificate.StartSerialNumber is not null && certificate.EndSerialNumber is not null
            ? $"{certificate.StartSerialNumber} - {certificate.EndSerialNumber}"
            : "—";

        // Request the natural, un-scaled module matrix (hint width/height of 1 keeps ZXing's internal scale
        // factor at 1x); the visual size is then controlled explicitly via moduleWidth/moduleHeight below,
        // avoiding a double-scale that would overflow its QuestPDF container.
        var barcodeMatrix = new Code128Writer().encode(certificate.CertificateNumber, BarcodeFormat.CODE_128, 1, 1);
        // Points at the public, unauthenticated verification page (Section: VerifyController) - scanning this
        // with a phone camera opens the certificate's authenticity check directly, no app or login required.
        var verificationUrl = Url.Action("Details", "Verify", new { certificateNumber = certificate.CertificateNumber }, Request.Scheme)
            ?? certificate.CertificateNumber;
        var qrMatrix = new QRCodeWriter().encode(verificationUrl, BarcodeFormat.QR_CODE, 1, 1);

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(24);
                page.DefaultTextStyle(x => x.FontSize(9).FontColor("#18324B"));

                page.Content().Row(mainRow =>
                {
                    // ---- Counterfoil stub ----
                    mainRow.ConstantItem(190).Background("#FBF8EF").Border(1).BorderColor("#E3D9BC").Padding(14).Column(stub =>
                    {
                        stub.Item().Text("CB BANK").Bold().FontSize(12).FontColor("#003B70");
                        stub.Item().PaddingTop(6).Text("Share Certificate").Bold().FontSize(10);
                        stub.Item().Text("Counterfoil").FontSize(7).FontColor("#8A7F60");

                        stub.Item().PaddingTop(10).Text("SHAREHOLDER ID").FontSize(6.5f).FontColor("#8A7F60");
                        stub.Item().Text(shareholder.ShareholderNo).Bold().FontSize(8.5f);

                        stub.Item().PaddingTop(4).Text("CERTIFICATE DATE").FontSize(6.5f).FontColor("#8A7F60");
                        stub.Item().Text(certificate.IssueDate.ToString("dd MMM yyyy")).Bold().FontSize(8.5f);

                        stub.Item().PaddingTop(4).Text("NAME").FontSize(6.5f).FontColor("#8A7F60");
                        stub.Item().Text(holderName ?? shareholder.ShareholderNo).Bold().FontSize(8.5f);

                        stub.Item().PaddingTop(4).Text("NRC / REGISTRATION NO.").FontSize(6.5f).FontColor("#8A7F60");
                        stub.Item().Text(nrcOrReg).Bold().FontSize(8.5f);

                        stub.Item().PaddingTop(4).Text("ADDRESS").FontSize(6.5f).FontColor("#8A7F60");
                        stub.Item().Text(address).FontSize(8f);

                        stub.Item().PaddingTop(4).Text("CERTIFICATE NO.").FontSize(6.5f).FontColor("#8A7F60");
                        stub.Item().Text(certificate.CertificateNumber).Bold().FontSize(8.5f);

                        stub.Item().PaddingTop(4).Text("SERIAL NO. (FROM - TO)").FontSize(6.5f).FontColor("#8A7F60");
                        stub.Item().Text(serialRange).Bold().FontSize(8.5f);

                        stub.Item().PaddingTop(4).Text("SHARES").FontSize(6.5f).FontColor("#8A7F60");
                        stub.Item().Text($"{certificate.Quantity:N0} {certificate.ShareClass?.NameEn}").Bold().FontSize(8.5f);

                        stub.Item().PaddingTop(20).Row(row =>
                        {
                            row.RelativeItem().Column(c => { c.Item().LineHorizontal(0.5f).LineColor("#C9BFA0"); c.Item().PaddingTop(2).Text("Director").FontSize(6.5f); });
                            row.ConstantItem(8);
                            row.RelativeItem().Column(c => { c.Item().LineHorizontal(0.5f).LineColor("#C9BFA0"); c.Item().PaddingTop(2).Text("Managing Director").FontSize(6.5f); });
                        });
                    });

                    mainRow.ConstantItem(10);

                    // ---- Main certificate ----
                    mainRow.RelativeItem().Column(main =>
                    {
                        main.Item().Height(6).Background("#003B70");
                        main.Item().PaddingTop(10).Row(row =>
                        {
                            row.RelativeItem().Column(c =>
                            {
                                c.Item().Text("CB BANK PUBLIC COMPANY LIMITED").Bold().FontSize(11).FontColor("#003B70");
                                c.Item().Text("Share Certificate").Bold().FontSize(15);
                            });
                            row.ConstantItem(90).Column(c =>
                            {
                                c.Item().AlignRight().Element(e => RenderBitMatrix(e, qrMatrix, 1.6f));
                            });
                        });

                        main.Item().PaddingTop(14).Row(row =>
                        {
                            row.RelativeItem().Column(c =>
                            {
                                c.Item().Text("SHAREHOLDER").FontSize(6.5f).FontColor("#8A7F60");
                                c.Item().Text(holderName ?? shareholder.ShareholderNo).Bold().FontSize(9.5f);
                                c.Item().PaddingTop(3).Text("NRC / REGISTRATION NO.").FontSize(6.5f).FontColor("#8A7F60");
                                c.Item().Text(nrcOrReg).Bold().FontSize(9.5f);
                                c.Item().PaddingTop(3).Text("ADDRESS").FontSize(6.5f).FontColor("#8A7F60");
                                c.Item().Text(address).FontSize(8.5f);
                                c.Item().PaddingTop(3).Text("SHARE SERIAL NO. (FROM - TO)").FontSize(6.5f).FontColor("#8A7F60");
                                c.Item().Text(serialRange).Bold().FontSize(9.5f);
                                c.Item().PaddingTop(3).Text("PAR VALUE / SHARE").FontSize(6.5f).FontColor("#8A7F60");
                                c.Item().Text("MMK 10,000").Bold().FontSize(9.5f);
                            });
                            row.ConstantItem(20);
                            row.RelativeItem().AlignRight().Column(c =>
                            {
                                c.Item().AlignRight().Text("SHARE CERTIFICATE NO.").FontSize(6.5f).FontColor("#8A7F60");
                                c.Item().AlignRight().Text(certificate.CertificateNumber).Bold().FontSize(9.5f);
                                c.Item().PaddingTop(3).AlignRight().Text("NO. OF SHARES").FontSize(6.5f).FontColor("#8A7F60");
                                c.Item().AlignRight().Text($"{certificate.Quantity:N0} shares").Bold().FontSize(9.5f);
                                c.Item().PaddingTop(3).AlignRight().Text("SHAREHOLDER ID").FontSize(6.5f).FontColor("#8A7F60");
                                c.Item().AlignRight().Text(shareholder.ShareholderNo).Bold().FontSize(9.5f);
                                c.Item().PaddingTop(3).AlignRight().Text("ISSUE DATE").FontSize(6.5f).FontColor("#8A7F60");
                                c.Item().AlignRight().Text(certificate.IssueDate.ToString("dd MMM yyyy")).Bold().FontSize(9.5f);
                            });
                        });

                        main.Item().PaddingTop(12).PaddingBottom(10).BorderTop(0.5f).BorderColor("#E3D9BC").PaddingTop(10)
                            .Text($"This is to certify the above person(s) is/are the registered holder(s) of the stated number of fully paid {certificate.ShareClass?.NameEn?.ToLowerInvariant()} of Ks-10,000/- each in the above stated company, subject to the Company Constitution of CB BANK PCL. Given under the common seal of CB BANK PCL.")
                            .FontSize(8.5f);

                        main.Item().Row(row =>
                        {
                            row.RelativeItem().Column(c =>
                            {
                                c.Item().Text("DATE OF ISSUE").FontSize(6.5f).FontColor("#8A7F60");
                                c.Item().Text(certificate.IssueDate.ToString("dd MMMM yyyy")).Bold().FontSize(9.5f);
                            });
                            row.ConstantItem(100).AlignCenter().Element(e => RenderSeal(e, "CB BANK PCL", "PUBLIC COMPANY LIMITED"));
                            row.RelativeItem().Column(c =>
                            {
                                c.Item().AlignRight().Row(r2 =>
                                {
                                    r2.RelativeItem().Column(cc => { cc.Item().LineHorizontal(0.5f).LineColor("#D9E1EA"); cc.Item().PaddingTop(2).AlignCenter().Text("Director").FontSize(7); });
                                    r2.ConstantItem(12);
                                    r2.RelativeItem().Column(cc => { cc.Item().LineHorizontal(0.5f).LineColor("#D9E1EA"); cc.Item().PaddingTop(2).AlignCenter().Text("Managing Director").FontSize(7); });
                                });
                            });
                        });

                        main.Item().PaddingTop(10).Element(e => RenderBitMatrix(e, barcodeMatrix, 0.8f, moduleHeight: 30));
                        main.Item().AlignCenter().Text(certificate.CertificateNumber).FontSize(8).FontColor("#5F7082");
                    });
                });

                page.Footer().Text("Confidential - CB Bank Internal Use. This document is not a negotiable instrument.").FontSize(7).FontColor("#98A6B4");
            });
        });

        var bytes = document.GeneratePdf();
        return File(bytes, "application/pdf", $"{certificate.CertificateNumber}.pdf");
    }

    /// <summary>Internal (not private) so <see cref="IssueSharesController"/>'s receipt PDF can reuse it.</summary>
    internal static string FormatAddress(Domain.Shareholders.Address? address)
    {
        if (address is null) return "—";
        var parts = new[] { address.Line1, string.IsNullOrWhiteSpace(address.Township) ? null : $"{address.Township} Township", address.City, address.StateRegion }
            .Where(p => !string.IsNullOrWhiteSpace(p));
        return string.Join(", ", parts);
    }

    /// <summary>Renders a ZXing BitMatrix (1D barcode or 2D QR) as a grid of filled cells using only QuestPDF's
    /// own layout primitives, so no image/native-imaging library is needed. Internal (not private) so
    /// <see cref="IssueSharesController"/>'s receipt PDF can reuse it for its own barcode.</summary>
    internal static void RenderBitMatrix(IContainer container, BitMatrix matrix, float moduleWidth, float? moduleHeight = null)
    {
        var height = moduleHeight ?? moduleWidth;
        container.Column(col =>
        {
            for (var y = 0; y < matrix.Height; y++)
            {
                col.Item().Row(row =>
                {
                    for (var x = 0; x < matrix.Width; x++)
                    {
                        row.ConstantItem(moduleWidth).Height(height).Background(matrix[x, y] ? "#18324B" : "#FFFFFF");
                    }
                });
            }
        });
    }

    /// <summary>A circular ink-stamp seal rendered as inline SVG (this QuestPDF version has no native rounded-
    /// rectangle/ellipse primitive), tilted slightly like a real stamp.</summary>
    internal static void RenderSeal(IContainer container, string topText, string bottomText)
    {
        const int size = 160;
        var r = size / 2 - 8;
        var cx = size / 2;
        var cy = size / 2;
        var svg = $"""
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {size} {size}" width="{size}" height="{size}">
              <circle cx="{cx}" cy="{cy}" r="{r}" fill="none" stroke="#1E5AA8" stroke-width="4" />
              <circle cx="{cx}" cy="{cy}" r="{r - 14}" fill="none" stroke="#1E5AA8" stroke-width="2" />
              <text x="{cx}" y="{cy - 12}" font-size="15" font-weight="700" fill="#1E5AA8" text-anchor="middle">{System.Net.WebUtility.HtmlEncode(topText)}</text>
              <text x="{cx}" y="{cy + 10}" font-size="10" font-weight="600" fill="#1E5AA8" text-anchor="middle">{System.Net.WebUtility.HtmlEncode(bottomText)}</text>
              <text x="{cx}" y="{cy + 32}" font-size="20" font-weight="800" fill="#1E5AA8" text-anchor="middle">*</text>
            </svg>
            """;

        container.Rotate(-6).Width(90).Height(90).Svg(svg);
    }
}

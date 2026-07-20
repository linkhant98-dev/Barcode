using EMS.Infrastructure.Persistence;
using EMS.Web.Models;
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

    /// <summary>Section 24 (print theme) - branded certificate PDF with a Code128 barcode and a QR verification code.</summary>
    public async Task<IActionResult> DownloadPdf(long id, CancellationToken ct)
    {
        var certificate = await _db.ShareCertificates
            .Include(c => c.Shareholder)!.ThenInclude(s => s!.Person)
            .Include(c => c.Shareholder)!.ThenInclude(s => s!.Corporate)
            .Include(c => c.ShareClass)
            .FirstOrDefaultAsync(c => c.Id == id, ct);
        if (certificate is null) return NotFound();

        var shareholder = certificate.Shareholder!;
        var holderName = shareholder.Type == Domain.Common.ApplicantType.Corporate
            ? shareholder.Corporate?.LegalNameEn
            : shareholder.Person?.NameEn;

        // Request the natural, un-scaled module matrix (hint width/height of 1 keeps ZXing's internal scale
        // factor at 1x); the visual size is then controlled explicitly via moduleWidth/moduleHeight below,
        // avoiding a double-scale that would overflow its QuestPDF container.
        var barcodeMatrix = new Code128Writer().encode(certificate.CertificateNumber, BarcodeFormat.CODE_128, 1, 1);
        var verificationPayload = $"{certificate.CertificateNumber}|{shareholder.ShareholderNo}|{certificate.Quantity:0}";
        var qrMatrix = new QRCodeWriter().encode(verificationPayload, BarcodeFormat.QR_CODE, 1, 1);

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A5.Landscape());
                page.Margin(28);
                page.DefaultTextStyle(x => x.FontSize(10).FontColor("#18324B"));

                page.Content().Column(col =>
                {
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text("EQUITY MANAGEMENT SYSTEM").Bold().FontSize(14).FontColor("#003B70");
                            c.Item().Text("CB Bank - Investor Relations").FontSize(9).FontColor("#5F7082");
                        });
                        row.ConstantItem(90).AlignRight().Text("CB").Bold().FontSize(22).FontColor("#004A8F");
                    });

                    col.Item().PaddingVertical(6).LineHorizontal(2).LineColor("#FDBB30");

                    col.Item().PaddingTop(8).AlignCenter().Text("SHARE CERTIFICATE").Bold().FontSize(16);
                    col.Item().AlignCenter().Text(certificate.CertificateNumber).FontSize(11).FontColor("#5F7082");

                    col.Item().PaddingTop(12).Row(row =>
                    {
                        row.RelativeItem(2).Column(c =>
                        {
                            c.Item().Text(t => { t.Span("Certifies that ").FontSize(10); t.Span(holderName ?? shareholder.ShareholderNo).Bold(); });
                            c.Item().PaddingTop(4).Text($"Shareholder ID: {shareholder.ShareholderNo}");
                            c.Item().Text($"Share Class: {certificate.ShareClass?.NameEn}");
                            c.Item().Text(t =>
                            {
                                t.Span("is the registered holder of ");
                                t.Span(certificate.Quantity.ToString("N0")).Bold();
                                t.Span(" fully paid ordinary shares.");
                            });
                            c.Item().PaddingTop(4).Text($"Issue Date: {certificate.IssueDate:dd MMM yyyy}");
                            c.Item().Text($"Status: {certificate.Status}");
                        });

                        row.ConstantItem(110).Column(c =>
                        {
                            c.Item().AlignCenter().Element(e => RenderBitMatrix(e, qrMatrix, 2.2f));
                            c.Item().AlignCenter().PaddingTop(2).Text("Scan to verify").FontSize(7).FontColor("#5F7082");
                        });
                    });

                    col.Item().PaddingTop(16).Element(e => RenderBitMatrix(e, barcodeMatrix, 1f, moduleHeight: 40));
                    col.Item().AlignCenter().Text(certificate.CertificateNumber).FontSize(9).FontColor("#5F7082");

                    col.Item().PaddingTop(16).Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().LineHorizontal(0.5f).LineColor("#D9E1EA");
                            c.Item().PaddingTop(2).Text("Authorized Signatory").FontSize(8).FontColor("#5F7082");
                        });
                        row.ConstantItem(20);
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().LineHorizontal(0.5f).LineColor("#D9E1EA");
                            c.Item().PaddingTop(2).Text("Company Secretary").FontSize(8).FontColor("#5F7082");
                        });
                    });
                });

                page.Footer().Text("Confidential - CB Bank Internal Use. This document is not a negotiable instrument.").FontSize(7).FontColor("#98A6B4");
            });
        });

        var bytes = document.GeneratePdf();
        return File(bytes, "application/pdf", $"{certificate.CertificateNumber}.pdf");
    }

    /// <summary>Renders a ZXing BitMatrix (1D barcode or 2D QR) as a grid of filled cells using only QuestPDF's
    /// own layout primitives, so no image/native-imaging library is needed.</summary>
    private static void RenderBitMatrix(IContainer container, BitMatrix matrix, float moduleWidth, float? moduleHeight = null)
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
}

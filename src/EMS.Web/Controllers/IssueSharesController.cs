using EMS.Application.Abstractions;
using EMS.Application.Shares;
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
using ZXing.OneD;

namespace EMS.Web.Controllers;

/// <summary>Section 7 - Issue Shares.</summary>
[Authorize]
public class IssueSharesController : Controller
{
    private readonly EmsDbContext _db;
    private readonly IIssueShareService _issueService;
    private readonly IPermissionService _permissions;

    public IssueSharesController(EmsDbContext db, IIssueShareService issueService, IPermissionService permissions)
    {
        _db = db;
        _issueService = issueService;
        _permissions = permissions;
    }

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        ViewData["Title"] = "Issue Shares";
        ViewData["Breadcrumb"] = new List<(string, string?)> { ("Dashboard", "/"), ("Share Operations", null), ("Issue Shares", null) };

        var transactions = await _db.ShareTransactions.Include(t => t.ShareIssue).ThenInclude(i => i!.Shareholder)
            .Where(t => t.Type == Domain.Common.ShareTransactionType.IssueShares)
            .OrderByDescending(t => t.Id).Take(100).ToListAsync(ct);

        return View(transactions);
    }

    /// <summary>Exports the full Issue Shares history (not just the 100-row on-screen preview) as CSV.</summary>
    public async Task<IActionResult> ExportCsv(CancellationToken ct)
    {
        var transactions = await _db.ShareTransactions.Include(t => t.ShareIssue)!.ThenInclude(i => i!.Shareholder)!.ThenInclude(s => s!.Person)
            .Include(t => t.ShareIssue)!.ThenInclude(i => i!.Shareholder)!.ThenInclude(s => s!.Corporate)
            .Include(t => t.ShareIssue)!.ThenInclude(i => i!.ShareClass)
            .Where(t => t.Type == Domain.Common.ShareTransactionType.IssueShares)
            .OrderByDescending(t => t.Id).ToListAsync(ct);

        var headers = new[] { "Reference", "Shareholder", "Class", "Quantity", "Amount", "Status" };
        var rows = transactions.Select(t =>
        {
            var sh = t.ShareIssue?.Shareholder;
            var name = sh?.Type == Domain.Common.ApplicantType.Corporate ? sh.Corporate?.LegalNameEn : sh?.Person?.NameEn;
            return (IReadOnlyList<object?>)new object?[] { t.TransactionNo, name, t.ShareIssue?.ShareClass?.NameEn, t.ShareIssue?.NumberOfShares, t.TotalAmount, t.Status.ToString() };
        });

        var bytes = CsvExportHelper.Build(headers, rows);
        return File(bytes, "text/csv", $"Issue-Shares-{DateTime.UtcNow:yyyyMMdd-HHmmss}.csv");
    }

    [HttpGet]
    public async Task<IActionResult> Create(long? shareholderId, CancellationToken ct)
    {
        ViewData["Title"] = "New Issue Shares";
        ViewData["Breadcrumb"] = new List<(string, string?)> { ("Dashboard", "/"), ("Issue Shares", Url.Action("Index")), ("New", null) };
        await PopulateLookupsAsync(ct);
        return View(new CreateIssueViewModel { ShareholderId = shareholderId ?? 0 });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateIssueViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            await PopulateLookupsAsync(ct);
            return View(model);
        }

        try
        {
            var transaction = await _issueService.CreateDraftAsync(new CreateIssueRequest(
                model.ApplyType, model.ShareholderId, model.ShareClassId, model.IssueDate,
                model.NumberOfShares, model.CapitalValuePerShare, model.PremiumValuePerShare,
                model.CashAmount, model.ChequeAmount, model.ChequeNumber, model.ChequeDate, null), ct);

            return RedirectToAction(nameof(Details), new { id = transaction.Id });
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            await PopulateLookupsAsync(ct);
            return View(model);
        }
    }

    public async Task<IActionResult> Details(long id, CancellationToken ct)
    {
        var transaction = await _db.ShareTransactions
            .Include(t => t.ShareIssue)!.ThenInclude(i => i!.Shareholder)!.ThenInclude(s => s!.Person)
            .Include(t => t.ShareIssue)!.ThenInclude(i => i!.ShareClass)
            .FirstOrDefaultAsync(t => t.Id == id, ct);
        if (transaction is null) return NotFound();

        ViewData["Title"] = transaction.TransactionNo;
        ViewData["Breadcrumb"] = new List<(string, string?)> { ("Dashboard", "/"), ("Issue Shares", Url.Action("Index")), (transaction.TransactionNo, null) };

        ViewBag.ApprovalInstance = transaction.ApprovalInstanceId is null
            ? null
            : await _db.ApprovalInstances.Include(a => a.Steps).FirstOrDefaultAsync(a => a.Id == transaction.ApprovalInstanceId, ct);

        return View(transaction);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Submit(long id, CancellationToken ct)
    {
        if (!await _permissions.CurrentUserHasPermissionAsync(Permissions.SubmitIssueShares, ct))
            return Forbid();

        await _issueService.SubmitAsync(id, ct);
        TempData["Success"] = "Issue submitted for approval.";
        return RedirectToAction(nameof(Details), new { id });
    }

    /// <summary>
    /// Receipt for a completed Issue Shares payment, matching the format learned from CB Bank's real receipt
    /// (header/address block, Capital + Premium + Total lines, cash/cheque, barcode, common seal). See the
    /// remark on <see cref="CertificatesController.DownloadPdf"/> for why this is English-only.
    /// </summary>
    public async Task<IActionResult> DownloadReceiptPdf(long id, CancellationToken ct)
    {
        var transaction = await _db.ShareTransactions
            .Include(t => t.ShareIssue)!.ThenInclude(i => i!.Shareholder)!.ThenInclude(s => s!.Person)
            .Include(t => t.ShareIssue)!.ThenInclude(i => i!.Shareholder)!.ThenInclude(s => s!.Corporate)
            .Include(t => t.ShareIssue)!.ThenInclude(i => i!.Shareholder)!.ThenInclude(s => s!.Addresses)
            .Include(t => t.ShareIssue)!.ThenInclude(i => i!.Shareholder)!.ThenInclude(s => s!.Contacts)
            .Include(t => t.ShareIssue)!.ThenInclude(i => i!.ShareClass)
            .FirstOrDefaultAsync(t => t.Id == id && t.Type == Domain.Common.ShareTransactionType.IssueShares, ct);
        if (transaction?.ShareIssue is null) return NotFound();

        var issue = transaction.ShareIssue;
        var shareholder = issue.Shareholder!;
        var holderName = shareholder.Type == Domain.Common.ApplicantType.Corporate
            ? shareholder.Corporate?.LegalNameEn
            : shareholder.Person?.NameEn;
        var nrcOrReg = shareholder.Person?.NrcNumber ?? shareholder.Corporate?.RegistrationNumber ?? "—";
        var address = CertificatesController.FormatAddress(shareholder.Addresses.FirstOrDefault());
        var email = shareholder.Contacts.FirstOrDefault(c => c.ContactType == Domain.Common.ContactType.Email)?.Value ?? "—";
        var phone = shareholder.Contacts.FirstOrDefault(c => c.ContactType == Domain.Common.ContactType.Mobile)?.Value ?? "—";

        var barcodeMatrix = new Code128Writer().encode(transaction.TransactionNo, BarcodeFormat.CODE_128, 1, 1);

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A5);
                page.Margin(28);
                page.DefaultTextStyle(x => x.FontSize(9).FontColor("#18324B"));

                page.Content().Column(col =>
                {
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text("CB BANK PCL").Bold().FontSize(13).FontColor("#003B70");
                            c.Item().Text("No. (46), Union Financial Center (Tower A, B, C), Corner of Mahar").FontSize(6.5f).FontColor("#5F7082");
                            c.Item().Text("Bandoola Road & Thein Phyu Road, Botahtaung Township, Yangon, 11161 Myanmar.").FontSize(6.5f).FontColor("#5F7082");
                            c.Item().Text("Tel: (95-1) 231 7999  Call Center: (95-1) 231 7770  contact@cbbank.com.mm").FontSize(6.5f).FontColor("#5F7082");
                        });
                        row.ConstantItem(100).AlignRight().Column(c =>
                        {
                            c.Item().AlignRight().Text("Date").FontSize(6.5f).FontColor("#8A7F60");
                            c.Item().AlignRight().Text(transaction.EffectiveDate.ToString("dd MMM yyyy")).Bold().FontSize(9);
                        });
                    });

                    col.Item().PaddingVertical(6).LineHorizontal(1.5f).LineColor("#FDBB30");

                    col.Item().PaddingTop(6).AlignCenter().Text($"Receipt for {issue.ShareClass?.NameEn} Purchase").Bold().FontSize(12);
                    col.Item().AlignCenter().Text(transaction.TransactionNo).FontSize(8).FontColor("#5F7082");

                    col.Item().PaddingTop(12).Text("Name").FontSize(6.5f).FontColor("#8A7F60");
                    col.Item().Text(holderName ?? shareholder.ShareholderNo).Bold().FontSize(9.5f);
                    col.Item().PaddingTop(3).Text("NRC / Registration No.").FontSize(6.5f).FontColor("#8A7F60");
                    col.Item().Text(nrcOrReg).Bold().FontSize(9.5f);
                    col.Item().PaddingTop(3).Text("Address").FontSize(6.5f).FontColor("#8A7F60");
                    col.Item().Text(address).FontSize(8.5f);
                    col.Item().PaddingTop(3).Text("Email / Phone").FontSize(6.5f).FontColor("#8A7F60");
                    col.Item().Text($"{email}  {phone}").FontSize(8.5f);

                    col.Item().PaddingTop(12).Table(table =>
                    {
                        table.ColumnsDefinition(c => { c.RelativeColumn(); c.ConstantColumn(120); });
                        void Row(string label, string value, bool bold = false)
                        {
                            var l = table.Cell().BorderBottom(0.5f).BorderColor("#E3D9BC").Padding(4).Text(label);
                            var v = table.Cell().BorderBottom(0.5f).BorderColor("#E3D9BC").Padding(4).AlignRight().Text(value);
                            if (bold) { l.Bold(); v.Bold(); }
                        }
                        Row($"Shares ({issue.NumberOfShares:N0}) — Capital value", $"MMK {issue.CapitalAmount:N0}");
                        Row($"Shares ({issue.NumberOfShares:N0}) — Premium value", $"MMK {issue.PremiumAmount:N0}");
                        Row("Total received", $"MMK {issue.TotalAmount:N0}", bold: true);
                    });

                    col.Item().PaddingTop(10).Text(
                        $"CB Bank PCL received full payment of the Capital and Premium value for {issue.NumberOfShares:N0} registered " +
                        $"{issue.ShareClass?.NameEn?.ToLowerInvariant()} of MMK {issue.CapitalValuePerShare:N0} par value each, on {transaction.EffectiveDate:dd MMM yyyy}."
                    ).FontSize(8);

                    col.Item().PaddingTop(8).Row(row =>
                    {
                        row.AutoItem().Text((issue.CashAmount > 0 ? "[x]" : "[ ]") + " By cash").FontSize(8.5f);
                        row.ConstantItem(24);
                        row.AutoItem().Text((issue.ChequeAmount > 0 ? "[x]" : "[ ]") + " By cheque").FontSize(8.5f);
                    });
                    if (!string.IsNullOrWhiteSpace(issue.ChequeNumber))
                        col.Item().PaddingTop(2).Text($"Cheque No.: {issue.ChequeNumber}").FontSize(8.5f);
                    col.Item().PaddingTop(2).Text($"Amount in words: Kyats {NumberToWords((long)issue.TotalAmount)} only /-").FontSize(8.5f).Italic();

                    col.Item().PaddingTop(16).Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Element(e => CertificatesController.RenderBitMatrix(e, barcodeMatrix, 0.5f, moduleHeight: 32));
                            c.Item().Text(transaction.TransactionNo).FontSize(7).FontColor("#5F7082");
                        });
                        row.ConstantItem(90).AlignCenter().Element(e => CertificatesController.RenderSeal(e, "CB BANK PCL", "INVESTOR RELATIONS"));
                        row.RelativeItem().AlignRight().AlignBottom().Column(c =>
                        {
                            c.Item().LineHorizontal(0.5f).LineColor("#D9E1EA");
                            c.Item().PaddingTop(2).Text("Authorized Signature").FontSize(7.5f);
                        });
                    });
                });

                page.Footer().Text("Confidential - CB Bank Internal Use.").FontSize(7).FontColor("#98A6B4");
            });
        });

        var bytes = document.GeneratePdf();
        return File(bytes, "application/pdf", $"Receipt-{transaction.TransactionNo}.pdf");
    }

    /// <summary>Minimal English number-to-words for the receipt's "Amount in words" line (whole Kyats only).</summary>
    private static string NumberToWords(long number)
    {
        if (number == 0) return "Zero";
        string[] ones = ["", "One", "Two", "Three", "Four", "Five", "Six", "Seven", "Eight", "Nine", "Ten",
            "Eleven", "Twelve", "Thirteen", "Fourteen", "Fifteen", "Sixteen", "Seventeen", "Eighteen", "Nineteen"];
        string[] tens = ["", "", "Twenty", "Thirty", "Forty", "Fifty", "Sixty", "Seventy", "Eighty", "Ninety"];

        string Chunk(long n)
        {
            if (n == 0) return "";
            if (n < 20) return ones[n] + " ";
            if (n < 100) return tens[n / 10] + " " + Chunk(n % 10);
            return ones[n / 100] + " Hundred " + Chunk(n % 100);
        }

        var words = "";
        var scales = new (long Value, string Name)[] { (1_000_000_000, "Billion"), (1_000_000, "Million"), (1_000, "Thousand") };
        foreach (var (value, name) in scales)
        {
            if (number >= value) { words += Chunk(number / value) + name + " "; number %= value; }
        }
        words += Chunk(number);
        return words.Trim();
    }

    private async Task PopulateLookupsAsync(CancellationToken ct)
    {
        ViewBag.Shareholders = await _db.Shareholders.Include(s => s.Person).Include(s => s.Corporate)
            .Where(s => s.Status == Domain.Common.ShareholderStatus.Active)
            .Select(s => new { s.Id, Label = s.ShareholderNo + " - " + (s.Corporate != null ? s.Corporate.LegalNameEn : s.Person!.NameEn) })
            .ToListAsync(ct);
        ViewBag.ShareClasses = await _db.ShareClasses.Where(c => c.IsActive).ToListAsync(ct);
    }
}

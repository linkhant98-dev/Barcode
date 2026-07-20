using EMS.Domain.Reporting;
using EMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EMS.Infrastructure.Jobs;

/// <summary>
/// Section 21.1 "Reconciliation" (spec target: daily end-of-day) and 15 "Reconciliation" control - share
/// ledger vs shareholder balance and certificate quantities. Writes one open ReconciliationResult row per
/// exception per business date (idempotent - re-running the same day does not duplicate rows).
/// </summary>
public class ReconciliationJob : BackgroundService
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(6);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ReconciliationJob> _logger;

    public ReconciliationJob(IServiceScopeFactory scopeFactory, ILogger<ReconciliationJob> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(CheckInterval);
        do
        {
            try
            {
                await RunOnceAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Reconciliation job run failed.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task RunOnceAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EmsDbContext>();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var exceptionsFound = 0;

        exceptionsFound += await CheckNegativeBalancesAsync(db, today, ct);
        exceptionsFound += await CheckDuplicateCertificatesAsync(db, today, ct);

        if (exceptionsFound > 0)
            _logger.LogWarning("Reconciliation job found {Count} exception(s) for {Date}.", exceptionsFound, today);
    }

    /// <summary>A shareholder/class running balance should never go negative; the transfer/issue services
    /// validate this at posting time, so a hit here indicates a bypass or a concurrency race worth investigating.</summary>
    private static async Task<int> CheckNegativeBalancesAsync(EmsDbContext db, DateOnly today, CancellationToken ct)
    {
        var ledgerRows = await db.ShareLedgerEntries
            .Where(l => !l.IsReversed)
            .Select(l => new { l.ShareholderId, l.ShareClassId, l.QuantityDelta })
            .ToListAsync(ct);

        var negativeBalances = ledgerRows
            .GroupBy(l => new { l.ShareholderId, l.ShareClassId })
            .Select(g => new { g.Key.ShareholderId, g.Key.ShareClassId, Balance = g.Sum(x => x.QuantityDelta) })
            .Where(g => g.Balance < 0)
            .ToList();

        var created = 0;
        foreach (var neg in negativeBalances)
        {
            var controlName = $"NegativeShareBalance:Shareholder{neg.ShareholderId}:Class{neg.ShareClassId}";
            if (await db.ReconciliationResults.AnyAsync(r => r.ControlName == controlName && r.BusinessDate == today, ct))
                continue;

            db.ReconciliationResults.Add(new ReconciliationResult
            {
                ControlName = controlName,
                BusinessDate = today,
                ExpectedValue = 0,
                ActualValue = neg.Balance,
                Difference = neg.Balance,
                Status = "Open",
                CreatedAtUtc = DateTime.UtcNow,
                CreatedBy = "system"
            });
            created++;
        }

        if (created > 0) await db.SaveChangesAsync(ct);
        return created;
    }

    /// <summary>IS-BR-07 - certificate numbers must be unique and non-overlapping among active certificates.</summary>
    private static async Task<int> CheckDuplicateCertificatesAsync(EmsDbContext db, DateOnly today, CancellationToken ct)
    {
        var certificateNumbers = await db.ShareCertificates
            .Where(c => c.Status != Domain.Common.CertificateStatus.Cancelled)
            .Select(c => c.CertificateNumber)
            .ToListAsync(ct);

        var duplicates = certificateNumbers.GroupBy(n => n).Where(g => g.Count() > 1).Select(g => g.Key).ToList();

        var created = 0;
        foreach (var certNumber in duplicates)
        {
            var controlName = $"DuplicateCertificateNumber:{certNumber}";
            if (await db.ReconciliationResults.AnyAsync(r => r.ControlName == controlName && r.BusinessDate == today, ct))
                continue;

            var count = certificateNumbers.Count(n => n == certNumber);
            db.ReconciliationResults.Add(new ReconciliationResult
            {
                ControlName = controlName,
                BusinessDate = today,
                ExpectedValue = 1,
                ActualValue = count,
                Difference = count - 1,
                Status = "Open",
                CreatedAtUtc = DateTime.UtcNow,
                CreatedBy = "system"
            });
            created++;
        }

        if (created > 0) await db.SaveChangesAsync(ct);
        return created;
    }
}

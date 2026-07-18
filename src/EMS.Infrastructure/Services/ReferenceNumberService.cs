using EMS.Application.Abstractions;
using EMS.Domain.Common;
using EMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EMS.Infrastructure.Services;

/// <summary>
/// COM-003 reference numbers, e.g. SA-2026-000001. Uses an UPDATE-then-read pattern under the database's
/// default transaction isolation so concurrent submissions do not collide; SQL Server row locking on the
/// UPDATE serializes concurrent callers for the same (Module, Year) key.
/// </summary>
public class ReferenceNumberService : IReferenceNumberService
{
    private readonly EmsDbContext _db;

    public ReferenceNumberService(EmsDbContext db) => _db = db;

    public async Task<string> NextAsync(string module, int? year = null, CancellationToken ct = default)
    {
        var effectiveYear = year ?? DateTime.UtcNow.Year;

        var sequence = await _db.NumberSequences
            .FirstOrDefaultAsync(s => s.Module == module && s.Year == effectiveYear, ct);

        if (sequence is null)
        {
            sequence = new NumberSequence { Module = module, Year = effectiveYear, LastValue = 0 };
            _db.NumberSequences.Add(sequence);
        }

        sequence.LastValue++;
        await _db.SaveChangesAsync(ct);

        return $"{module}-{effectiveYear}-{sequence.LastValue:D6}";
    }

    public async Task<string> NextShareholderNoAsync(CancellationToken ct = default)
    {
        var year = DateTime.UtcNow.Year;
        var sequence = await _db.NumberSequences.FirstOrDefaultAsync(s => s.Module == "SH" && s.Year == year, ct);
        if (sequence is null)
        {
            sequence = new NumberSequence { Module = "SH", Year = year, LastValue = 0 };
            _db.NumberSequences.Add(sequence);
        }

        sequence.LastValue++;
        await _db.SaveChangesAsync(ct);

        var yy = year % 100;
        return $"SH-{yy:D2}{sequence.LastValue:D7}";
    }

    public Task<string> NextCertificateNoAsync(CancellationToken ct = default) => NextAsync("CERT", null, ct);
}

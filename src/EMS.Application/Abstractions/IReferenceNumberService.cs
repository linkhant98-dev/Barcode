namespace EMS.Application.Abstractions;

/// <summary>COM-003 - generates unique, gap-tolerant reference numbers such as SA-2026-000001.</summary>
public interface IReferenceNumberService
{
    Task<string> NextAsync(string module, int? year = null, CancellationToken ct = default);

    /// <summary>KYC-007 - shareholder ID format, e.g. SH-250000001 (SH- + 2-digit year + 7-digit running number).</summary>
    Task<string> NextShareholderNoAsync(CancellationToken ct = default);

    /// <summary>Appendix B - certificate numbers, e.g. CERT-2026-000001.</summary>
    Task<string> NextCertificateNoAsync(CancellationToken ct = default);
}

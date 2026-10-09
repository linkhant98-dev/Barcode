namespace EMS.Domain.Common;

/// <summary>COM-003 - backs generation of SA-YYYY-NNNNNN / IS- / TS- / BS- / DS- style reference numbers per module per year.</summary>
public class NumberSequence
{
    public long NumberSequenceId { get; set; }
    public string Module { get; set; } = string.Empty; // SA, IS, TS, BS, DS, SH, CERT
    public int Year { get; set; }
    public long LastValue { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}

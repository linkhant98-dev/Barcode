using System.Text;

namespace EMS.Web.Services;

/// <summary>Shared CSV writer for the list-screen "Export" actions (Shareholders, Certificates, Issue/Transfer
/// Shares, Bonus, Dividends, Applications, Audit Log). Reports already export via ClosedXML/QuestPDF
/// (<see cref="Controllers.ReportsController"/>); these simpler list exports use plain CSV so every list
/// screen has a working, dependency-free download of its full result set.</summary>
public static class CsvExportHelper
{
    public static byte[] Build(IReadOnlyList<string> headers, IEnumerable<IReadOnlyList<object?>> rows)
    {
        var sb = new StringBuilder();
        sb.Append('﻿');
        sb.AppendLine(string.Join(',', headers.Select(Escape)));
        foreach (var row in rows)
            sb.AppendLine(string.Join(',', row.Select(v => Escape(v?.ToString() ?? string.Empty))));

        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    private static string Escape(string value) =>
        value.IndexOfAny([',', '"', '\n', '\r']) < 0 ? value : '"' + value.Replace("\"", "\"\"") + '"';
}

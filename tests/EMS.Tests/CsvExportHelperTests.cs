using System.Text;
using EMS.Web.Services;
using Xunit;

namespace EMS.Tests;

public class CsvExportHelperTests
{
    private static string Decode(byte[] bytes) => Encoding.UTF8.GetString(bytes);

    [Fact]
    public void Build_SimpleRow_ProducesCommaSeparatedLineWithUtf8Bom()
    {
        var bytes = CsvExportHelper.Build(["Name", "Amount"], [["Alice", 100]]);

        Assert.Equal((byte)0xEF, bytes[0]);
        Assert.Equal((byte)0xBB, bytes[1]);
        Assert.Equal((byte)0xBF, bytes[2]);
        var text = Decode(bytes);
        Assert.Contains("Name,Amount", text);
        Assert.Contains("Alice,100", text);
    }

    [Fact]
    public void Build_ValueContainingComma_IsWrappedInQuotes()
    {
        var bytes = CsvExportHelper.Build(["Address"], [["No. 1, Main Road"]]);

        Assert.Contains("\"No. 1, Main Road\"", Decode(bytes));
    }

    [Fact]
    public void Build_ValueContainingDoubleQuote_EscapesAsDoubledQuoteAndWraps()
    {
        var bytes = CsvExportHelper.Build(["Note"], [["He said \"hello\""]]);

        Assert.Contains("\"He said \"\"hello\"\"\"", Decode(bytes));
    }

    [Fact]
    public void Build_ValueContainingNewline_IsWrappedInQuotes()
    {
        var bytes = CsvExportHelper.Build(["Note"], [["Line1\nLine2"]]);

        Assert.Contains("\"Line1\nLine2\"", Decode(bytes));
    }

    [Fact]
    public void Build_ValueWithNoSpecialCharacters_IsNotQuoted()
    {
        var bytes = CsvExportHelper.Build(["Name"], [["PlainValue"]]);

        Assert.DoesNotContain("\"PlainValue\"", Decode(bytes));
    }

    [Fact]
    public void Build_NullValue_BecomesEmptyStringNotTheWordNull()
    {
        var bytes = CsvExportHelper.Build(["Name", "Note"], [["Alice", null]]);

        var text = Decode(bytes);
        Assert.Contains("Alice," + Environment.NewLine, text);
        Assert.DoesNotContain("null", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Build_NoRows_ProducesOnlyTheHeaderLine()
    {
        var bytes = CsvExportHelper.Build(["Name", "Amount"], []);

        var text = Decode(bytes).TrimStart('﻿');
        Assert.Equal("Name,Amount" + Environment.NewLine, text);
    }
}

using System.Globalization;
using System.Text;
using MealTrace.Application.Features.Reports;
using Microsoft.VisualBasic.FileIO;
using Cell = MealTrace.Application.Features.Reports.ReportCsvWriter.Cell;

namespace MealTrace.Api.Tests;

public class ReportCsvWriterTests
{
    [Fact]
    public void WritesUtf8WithBomAndRoundTripsVietnameseText()
    {
        var csv = ReportCsvWriter.WriteUtf8(["Supplier", "Nguồn thực tế"],
            [[Cell.Text("Local farm"), Cell.Text("Bữa trưa – rau củ từ Đà Lạt")]]);

        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, csv[..3]);
        Assert.Equal("\"Supplier\",\"Nguồn thực tế\"\r\n\"Local farm\",\"Bữa trưa – rau củ từ Đà Lạt\"\r\n",
            new UTF8Encoding(false, true).GetString(csv[3..]));
        Assert.Collection(Parse(csv),
            header => Assert.Equal(new[] { "Supplier", "Nguồn thực tế" }, header),
            row => Assert.Equal(new[] { "Local farm", "Bữa trưa – rau củ từ Đà Lạt" }, row));
    }

    [Fact]
    public void RoundTripsQuotedFieldsIncludingLineBreaksAndWhitespace()
    {
        string[] values = ["Rau, củ", "Phiếu \"A\"", "a\rb", "a\nb", "a\r\nb", " \t Rau củ \t ", " \t "];
        var row = values.Select(Cell.Text).ToArray();
        var csv = ReportCsvWriter.WriteUtf8(["Comma", "Quote", "CR", "LF", "CRLF", "Spaces", "Whitespace"], [row]);

        Assert.Equal(values, Parse(csv)[1]);
    }

    [Fact]
    public void WritesNullEmptyAndDefaultCellsAsEmptyFields()
    {
        var csv = ReportCsvWriter.WriteUtf8(["Null text", "Empty text", "Null number", "Default"],
            [[Cell.Text(null), Cell.Text(""), Cell.Number(null), default]]);

        Assert.Equal(new[] { "", "", "", "" }, Parse(csv)[1]);
    }

    [Theory]
    [InlineData("=1+1")]
    [InlineData("+1+1")]
    [InlineData("-1.25")]
    [InlineData("@SUM(1,2)")]
    [InlineData("\t=1+1")]
    [InlineData("\n+1+1")]
    [InlineData("\r\n -1.25")]
    [InlineData(" \u00A0@SUM(1,2)")]
    [InlineData("\u001F\u200B\uFEFF=1+1")]
    [InlineData("\u200B＝1+1")]
    public void NeutralizesFormulaPrefixesInTextAndHeaders(string attempt)
    {
        var csv = ReportCsvWriter.WriteUtf8([attempt], [[Cell.Text(attempt)]]);

        Assert.Collection(Parse(csv),
            header => Assert.Equal("'" + attempt, Assert.Single(header)),
            row => Assert.Equal("'" + attempt, Assert.Single(row)));
    }

    [Fact]
    public void KeepsMaliciousSupplierNamesAndSourceReferencesInsideTheirOwnFields()
    {
        const string supplier = "=SUM(1,1),\"A\"\nB";
        const string reference = "\t@SUM(1,2)\r\n\"ref\",=1+1";
        const string ordinaryReference = "Phiếu \"X\",=1+1\r\n\"next\"";
        var csv = ReportCsvWriter.WriteUtf8(["Supplier", "Source reference"],
            [[Cell.Text(supplier), Cell.Text(reference)], [Cell.Text("Vườn A"), Cell.Text(ordinaryReference)]]);

        Assert.Collection(Parse(csv),
            header => Assert.Equal(new[] { "Supplier", "Source reference" }, header),
            first => Assert.Equal(new[] { "'" + supplier, "'" + reference }, first),
            second => Assert.Equal(new[] { "Vườn A", ordinaryReference }, second));
    }

    [Fact]
    public void PreservesNumericPrecisionAndInputOrderAcrossCultures()
    {
        string[] headers = ["Quantity", "Portions", "Supplier"];
        Cell[][] rows =
        [
            [Cell.Number(-1234.5678901234567890123456780m), Cell.Number(-42), Cell.Text("Vườn B")],
            [Cell.Number(0.0100m), Cell.Number(7), Cell.Text("Vườn A")]
        ];

        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("vi-VN");
            var csv = ReportCsvWriter.WriteUtf8(headers, rows);
            Assert.Equal(csv, ReportCsvWriter.WriteUtf8(headers, rows));
            Assert.Equal("\"Quantity\",\"Portions\",\"Supplier\"\r\n-1234.5678901234567890123456780,-42,\"Vườn B\"\r\n0.0100,7,\"Vườn A\"\r\n",
                Encoding.UTF8.GetString(csv[3..]));

            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
            Assert.Equal(csv, ReportCsvWriter.WriteUtf8(headers, rows));
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public void RejectsRowsWithTheWrongNumberOfColumns()
    {
        Assert.Throws<ArgumentException>("rows", () =>
            ReportCsvWriter.WriteUtf8(["Supplier", "Quantity"], [[Cell.Text("Vườn A")]]));
    }

    private static List<string[]> Parse(byte[] csv)
    {
        using var parser = new TextFieldParser(new MemoryStream(csv), Encoding.UTF8)
        {
            HasFieldsEnclosedInQuotes = true,
            TrimWhiteSpace = false
        };
        parser.SetDelimiters(",");

        var rows = new List<string[]>();
        while (!parser.EndOfData)
            rows.Add(parser.ReadFields()!);

        return rows;
    }
}

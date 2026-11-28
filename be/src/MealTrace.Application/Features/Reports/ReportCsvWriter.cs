using System.Globalization;
using System.Text;

namespace MealTrace.Application.Features.Reports;

public static class ReportCsvWriter
{
    /// <summary>Writes a UTF-8 CSV with BOM and CRLF, preserving the supplied column and row order.</summary>
    public static byte[] WriteUtf8(IReadOnlyList<string?> headers, IEnumerable<IReadOnlyList<Cell>> rows)
    {
        ArgumentNullException.ThrowIfNull(headers);
        ArgumentNullException.ThrowIfNull(rows);
        if (headers.Count == 0)
            throw new ArgumentException("At least one CSV column is required.", nameof(headers));

        using var output = new MemoryStream();
        using (var writer = new StreamWriter(output, new UTF8Encoding(true, true), leaveOpen: true))
        {
            writer.NewLine = "\r\n";
            writer.WriteLine(string.Join(',', headers.Select(header => Cell.Text(header).CsvValue)));
            foreach (var row in rows)
            {
                if (row is null)
                    throw new ArgumentException("CSV rows cannot contain null rows.", nameof(rows));
                if (row.Count != headers.Count)
                    throw new ArgumentException("Each CSV row must have the same number of columns as the header.", nameof(rows));

                writer.WriteLine(string.Join(',', row.Select(cell => cell.CsvValue)));
            }
        }

        return output.ToArray();
    }

    public readonly struct Cell
    {
        private readonly string? _csvValue;

        private Cell(string? csvValue) => _csvValue = csvValue;

        internal string CsvValue => _csvValue ?? "\"\"";

        /// <summary>Quotes untrusted text and prefixes potential spreadsheet formulas with an apostrophe.</summary>
        public static Cell Text(string? value)
        {
            if (string.IsNullOrEmpty(value))
                return default;

            var prefix = StartsWithFormula(value) ? "'" : "";
            return new Cell("\"" + prefix + value.Replace("\"", "\"\"") + "\"");
        }

        /// <summary>Writes a trusted decimal or integer using invariant formatting. Null writes an empty cell.</summary>
        public static Cell Number(decimal? value) => new(value?.ToString(CultureInfo.InvariantCulture));

        private static bool StartsWithFormula(string value)
        {
            // Spreadsheet importers can ignore leading whitespace, controls and invisible format characters.
            foreach (var rune in value.EnumerateRunes())
            {
                if (Rune.IsWhiteSpace(rune) || Rune.IsControl(rune) || Rune.GetUnicodeCategory(rune) == UnicodeCategory.Format)
                    continue;

                return rune.Value is '=' or '+' or '-' or '@' or '＝' or '＋' or '－' or '＠';
            }

            return false;
        }
    }
}

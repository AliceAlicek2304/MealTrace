using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using MealTrace.Application.Abstractions;
using MealTrace.Application.Dtos.Students;

namespace MealTrace.Infrastructure.Imports;

/// <summary>Bounded, read-only OOXML reader. Never evaluates formulas or follows external relationships.</summary>
public sealed class StudentSpreadsheetReader : IStudentSpreadsheetReader
{
    private static readonly XNamespace Main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly XNamespace Relationships = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private static readonly HashSet<string> NameHeaders = ["ho ten", "ho va ten", "ho ten tre", "ho va ten tre", "ho ten hoc sinh", "ho va ten hoc sinh", "ten tre"];
    private static readonly HashSet<string> BirthHeaders = ["ngay sinh", "ngay thang nam sinh", "ngay thang sinh", "date of birth", "dob"];
    private static readonly HashSet<string> GenderHeaders = ["gioi tinh", "gender", "sex"];
    private static readonly HashSet<string> PhoneHeaders = ["sdt", "so dt", "so dien thoai", "so dien thoai lien he", "dien thoai", "sdt phu huynh", "so dien thoai phu huynh", "sdt lien he", "dien thoai phu huynh", "phone", "phone number"];

    public StudentImportSheet Read(Stream file)
    {
        try { return ReadWorkbook(file); }
        catch (InvalidStudentSpreadsheetException) { throw; }
        catch (Exception ex) when (ex is InvalidDataException or XmlException or IOException or ArgumentException or OverflowException or InvalidOperationException)
        { throw new InvalidStudentSpreadsheetException("Không đọc được XLSX. Hãy lưu lại bằng Excel và tải lại file hợp lệ."); }
    }

    private static StudentImportSheet ReadWorkbook(Stream file)
    {
        using var archive = new ZipArchive(file, ZipArchiveMode.Read, leaveOpen: true);
        if (archive.Entries.Count > 2000 || archive.Entries.Sum(entry => entry.Length) > 20 * 1024 * 1024 ||
            archive.Entries.Any(entry => entry.FullName.EndsWith("vbaProject.bin", StringComparison.OrdinalIgnoreCase)))
            throw Invalid("File quá lớn sau giải nén hoặc chứa macro. Chỉ dùng XLSX tối đa 5 MB, 500 trẻ.");
        var workbook = Load(archive, "xl/workbook.xml");
        var sheets = workbook.Descendants(Main + "sheet").ToArray();
        if (sheets.Length != 1) throw Invalid("Mỗi lần nhập dùng file có đúng một sheet danh sách trẻ.");
        var relationshipId = (string?)sheets[0].Attribute(Relationships + "id");
        var relationship = Load(archive, "xl/_rels/workbook.xml.rels").Root?.Elements()
            .SingleOrDefault(element => (string?)element.Attribute("Id") == relationshipId);
        if (relationship is null || (string?)relationship.Attribute("TargetMode") == "External") throw Invalid("Sheet không hợp lệ.");
        var target = (string?)relationship.Attribute("Target") ?? "";
        if (target.Contains("..") || target.Contains('\\') || target.Contains(':')) throw Invalid("Đường dẫn sheet không hợp lệ.");
        var sheetPath = target.StartsWith('/') ? target.TrimStart('/') : "xl/" + target;
        var strings = archive.GetEntry("xl/sharedStrings.xml") is null ? [] : Load(archive, "xl/sharedStrings.xml")
            .Descendants(Main + "si").Select(element => string.Concat(element.Descendants(Main + "t").Select(t => t.Value))).ToArray();
        var sheet = Load(archive, sheetPath);
        var rows = sheet.Descendants(Main + "sheetData").Elements(Main + "row").ToArray();
        if (rows.Length > 2000 || sheet.Descendants(Main + "c").Count() > 100000) throw Invalid("File vượt giới hạn. Mỗi lần tối đa 500 trẻ.");
        var decoded = rows.Select((row, index) => new
        {
            Number = int.TryParse((string?)row.Attribute("r"), out var number) ? number : index + 1,
            Cells = row.Elements(Main + "c").Select(cell => Decode(cell, strings)).ToArray()
        }).ToArray();
        var header = decoded.Take(30).FirstOrDefault(row => row.Cells.Any(cell => NameHeaders.Contains(Key(cell.Text))));
        if (header is null) throw Invalid("Không tìm thấy cột Họ tên trong 30 dòng đầu. Dùng tiêu đề Họ tên hoặc Họ tên trẻ.");
        var columns = header.Cells.Where(cell => NameHeaders.Contains(Key(cell.Text))).ToArray();
        if (columns.Length != 1) throw Invalid("File có nhiều cột Họ tên. Chỉ giữ một cột họ tên trẻ.");
        var nameColumn = columns[0].Column;
        string? OptionalColumn(HashSet<string> aliases)
        {
            var matches = header.Cells.Where(cell => aliases.Contains(Key(cell.Text))).ToArray();
            if (matches.Length > 1) throw Invalid("File có nhiều cột cho cùng một thông tin. Chỉ giữ một cột ngày sinh, giới tính hoặc SĐT phụ huynh.");
            return matches.SingleOrDefault()?.Column;
        }
        var birthColumn = OptionalColumn(BirthHeaders);
        var genderColumn = OptionalColumn(GenderHeaders);
        var phoneColumn = OptionalColumn(PhoneHeaders);
        var date1904 = (string?)workbook.Root?.Element(Main + "workbookPr")?.Attribute("date1904") is "1" or "true";
        var output = new List<StudentImportRow>();
        foreach (var row in decoded.Where(row => row.Number > header.Number))
        {
            if (row.Cells.All(cell => string.IsNullOrWhiteSpace(cell.Text) && !cell.Formula)) continue;
            var cell = row.Cells.SingleOrDefault(cell => cell.Column == nameColumn);
            var name = string.Join(' ', (cell?.Text ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).Normalize(NormalizationForm.FormC);
            var error = cell?.Formula == true ? "Họ tên phải là văn bản, không dùng công thức." :
                name.Length == 0 ? "Thiếu họ tên trẻ." : name.Length > 150 || name.Any(char.IsControl) ? "Họ tên tối đa 150 ký tự và không chứa ký tự điều khiển." : null;
            var birthCell = row.Cells.SingleOrDefault(cell => cell.Column == birthColumn);
            var genderCell = row.Cells.SingleOrDefault(cell => cell.Column == genderColumn);
            DateOnly? birth = null;
            string? gender = null;
            if (birthCell?.Formula == true || genderCell?.Formula == true)
                error ??= "Ngày sinh và giới tính không được dùng công thức.";
            if (!string.IsNullOrWhiteSpace(birthCell?.Text) && birthCell.Formula == false)
            {
                birth = ParseBirth(birthCell, date1904);
                if (birth is null) error ??= "Ngày sinh không hợp lệ. Dùng ngày Excel hoặc dd/MM/yyyy.";
            }
            if (!string.IsNullOrWhiteSpace(genderCell?.Text) && genderCell.Formula == false)
            {
                gender = Key(genderCell.Text) switch { "nam" or "male" or "m" => "MALE", "nu" or "female" or "f" => "FEMALE", "khac" or "other" => "OTHER", _ => null };
                if (gender is null) error ??= "Giới tính phải là Nam, Nữ hoặc Khác.";
            }
            var phoneCell = row.Cells.SingleOrDefault(cell => cell.Column == phoneColumn);
            // Display the supplied contact only; do not create a parent, send a message or evaluate formulas.
            var phone = phoneCell?.Formula == false && !string.IsNullOrWhiteSpace(phoneCell.Text) ? phoneCell.Text.Trim() : null;
            output.Add(new StudentImportRow(row.Number, name, error, birth, gender, phone));
            if (output.Count > 500) throw Invalid("Mỗi lần nhập tối đa 500 trẻ. Hãy chia nhỏ file.");
        }
        if (output.Count == 0) throw Invalid("File không có trẻ để nhập.");
        return new StudentImportSheet((string?)sheets[0].Attribute("name") ?? "Danh sách",
            header.Cells.Where(cell => cell.Column != nameColumn && cell.Column != birthColumn && cell.Column != genderColumn && !string.IsNullOrWhiteSpace(cell.Text)).Select(cell => cell.Text).ToArray(), output.ToArray());
    }

    private static DateOnly? ParseBirth(Cell cell, bool date1904)
    {
        if (cell.Numeric && double.TryParse(cell.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var serial))
        {
            // Excel's 1900 calendar contains a fictitious February 29 (serial 60).
            if (!double.IsFinite(serial) || serial < 0 || serial > 2958465 || (!date1904 && serial == 60)) return null;
            try
            {
                var epoch = date1904 ? new DateTime(1904, 1, 1) : new DateTime(1899, 12, 31);
                var days = !date1904 && serial > 60 ? serial - 1 : serial;
                return DateOnly.FromDateTime(epoch.AddDays(days));
            }
            catch (ArgumentOutOfRangeException) { return null; }
        }
        return DateOnly.TryParseExact(cell.Text.Trim(), ["d/M/yyyy", "dd/MM/yyyy", "d-M-yyyy", "dd-MM-yyyy", "yyyy-MM-dd"],
            CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ? date : null;
    }

    private sealed record Cell(string Column, string Text, bool Formula, bool Numeric);
    private static Cell Decode(XElement cell, string[] strings)
    {
        var address = (string?)cell.Attribute("r") ?? "";
        var column = new string(address.TakeWhile(char.IsAsciiLetter).ToArray());
        if (column.Length is 0 or > 3) throw Invalid("Địa chỉ ô Excel không hợp lệ.");
        var type = (string?)cell.Attribute("t");
        var value = cell.Element(Main + "v")?.Value ?? "";
        if (type == "s")
        {
            if (!int.TryParse(value, out var index) || index < 0 || index >= strings.Length) throw Invalid("Bảng chuỗi Excel không hợp lệ.");
            value = strings[index];
        }
        else if (type == "inlineStr") value = string.Concat(cell.Descendants(Main + "t").Select(t => t.Value));
        return new Cell(column, value, cell.Element(Main + "f") is not null, type is null or "n");
    }

    private static XDocument Load(ZipArchive archive, string name)
    {
        var entry = archive.GetEntry(name) ?? throw Invalid("Thiếu thành phần bắt buộc của XLSX.");
        using var stream = entry.Open();
        using var xml = XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null,
            MaxCharactersInDocument = 20 * 1024 * 1024 });
        return XDocument.Load(xml);
    }

    private static string Key(string value) => string.Join(' ', new string(value.ToLowerInvariant().Replace('đ', 'd')
        .Normalize(NormalizationForm.FormD).Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).ToArray())
        .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    private static InvalidStudentSpreadsheetException Invalid(string message) => new(message);
}

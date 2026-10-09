using System.IO.Compression;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Xml.Linq;
using MealTrace.Application.Abstractions;
using MealTrace.Infrastructure.Imports;
using MealTrace.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static MealTrace.Api.Tests.AuthenticationTests;

namespace MealTrace.Api.Tests;

public sealed class StudentImportTests
{
    [Fact] public Task AdminImportsOnlyChildrenWithEnrollmentAndCannotReplay() => Flow(false);
    [PostgresFact] public Task PostgreSqlImportsOnlyChildrenWithEnrollmentAndCannotReplay() => Flow(true);

    [PostgresFact]
    public async Task ConcurrentConfirmationsCannotDuplicateAClassRoster()
    {
        using var factory = new AuthTestFactory(postgres: true);
        var seed = await factory.SeedUsersAsync();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", await LoginAsync(client, seed.AdminEmail, seed.Password));
        var file = Workbook(["Nguyễn An", "Trần Bình"]);
        var responses = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ => client.PostAsync("/api/admin/students/import/confirm", Upload(file, seed.ClassId))));
        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.OK);
        Assert.All(responses.Where(response => response.StatusCode != HttpStatusCode.OK), response =>
            Assert.Contains(response.StatusCode, new[] { HttpStatusCode.BadRequest, HttpStatusCode.Conflict }));
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MealTraceDbContext>();
        Assert.Equal(2, await db.Students.CountAsync());
        Assert.Equal(2, await db.Enrollments.CountAsync());
    }

    private static async Task Flow(bool postgres)
    {
        using var factory = new AuthTestFactory(postgres);
        var seed = await factory.SeedUsersAsync();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", await LoginAsync(client, seed.AdminEmail, seed.Password));
        var bytes = Workbook(["Nguyễn An", "Trần Bình"]);
        var preview = await client.PostAsync("/api/admin/students/import/preview", Upload(bytes, seed.ClassId));
        Assert.Equal(HttpStatusCode.OK, preview.StatusCode);
        var body = await preview.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("canImport").GetBoolean());
        Assert.Equal(2, body.GetProperty("rows").GetArrayLength());
        Assert.Equal("2024-01-01", body.GetProperty("rows")[0].GetProperty("dateOfBirth").GetString());
        Assert.Equal("FEMALE", body.GetProperty("rows")[0].GetProperty("gender").GetString());
        Assert.Equal("0900000001", body.GetProperty("rows")[0].GetProperty("parentPhoneNumber").GetString());
        Assert.Contains("Số điện thoại liên hệ", body.GetProperty("ignoredColumns").EnumerateArray().Select(x => x.GetString()));
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MealTraceDbContext>();
            Assert.Equal(0, await db.Students.CountAsync());
        }
        var confirmed = await client.PostAsync("/api/admin/students/import/confirm", Upload(bytes, seed.ClassId));
        Assert.Equal(HttpStatusCode.OK, confirmed.StatusCode);
        var result = await confirmed.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, result.GetProperty("created").GetInt32());
        var batchId = result.GetProperty("batchId").GetGuid();
        var history = await client.GetFromJsonAsync<JsonElement>($"/api/admin/students/import/history?classId={seed.ClassId}&pageSize=1");
        Assert.Equal(1, history.GetProperty("total").GetInt32());
        Assert.Single(history.GetProperty("items").EnumerateArray());
        Assert.Equal(batchId, history.GetProperty("items")[0].GetProperty("id").GetGuid());
        Assert.Equal("test.xlsx", history.GetProperty("items")[0].GetProperty("fileName").GetString());
        var detail = await client.GetFromJsonAsync<JsonElement>($"/api/admin/students/import/history/{batchId}");
        Assert.Equal(2, detail.GetProperty("rows").GetArrayLength());
        Assert.Equal("2024-01-01", detail.GetProperty("rows")[0].GetProperty("dateOfBirth").GetString());
        Assert.Equal(0, (await client.GetFromJsonAsync<JsonElement>($"/api/admin/students/import/history?classId={Guid.NewGuid()}")).GetProperty("total").GetInt32());
        Assert.Empty((await client.GetFromJsonAsync<JsonElement>("/api/admin/students/import/history?page=2&pageSize=1")).GetProperty("items").EnumerateArray());
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/admin/students/import/history/{Guid.NewGuid()}")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/api/admin/students/import/confirm", Upload(bytes, seed.ClassId))).StatusCode);
        // One existing name makes the whole new batch invalid; the other row must not be partially saved.
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/api/admin/students/import/confirm", Upload(Workbook(["Nguyễn An", "Lê Châu"]), seed.ClassId))).StatusCode);
        using var finalScope = factory.Services.CreateScope();
        var finalDb = finalScope.ServiceProvider.GetRequiredService<MealTraceDbContext>();
        var students = await finalDb.Students.Include(x => x.Enrollments).ToListAsync();
        Assert.Equal(2, students.Count);
        Assert.All(students, student => { Assert.Equal(new DateOnly(2024, 1, 1), student.DateOfBirth); Assert.Equal("FEMALE", student.Gender); });
        Assert.All(students, student => { Assert.StartsWith("HS-", student.StudentCode); Assert.Equal(seed.ClassId, student.ClassId); Assert.Equal(seed.ClassId, Assert.Single(student.Enrollments).ClassId); });
        Assert.Equal(0, await finalDb.ParentStudents.CountAsync());
        Assert.Equal(2, await finalDb.Users.CountAsync());
        Assert.Single(await finalDb.StudentImportBatches.ToListAsync());
        var child = students[0]; child.FullName = "Hồ sơ đã sửa"; await finalDb.SaveChangesAsync();
        var unchanged = await client.GetFromJsonAsync<JsonElement>($"/api/admin/students/import/history/{batchId}");
        Assert.DoesNotContain("Hồ sơ đã sửa", unchanged.GetRawText());
        client.DefaultRequestHeaders.Authorization = new("Bearer", await LoginAsync(client, seed.TeacherEmail, seed.Password));
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/admin/students/import/history")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/api/admin/students/import/history/{batchId}")).StatusCode);
    }

    [Theory]
    [InlineData("anonymous")]
    [InlineData("teacher")]
    [InlineData("invalid-class")]
    [InlineData("past-date")]
    [InlineData("invalid-file")]
    [InlineData("wrong-extension")]
    [InlineData("duplicate")]
    [InlineData("formula")]
    [InlineData("missing-name")]
    public async Task InvalidOrUnauthorizedImportsNeverWrite(string scenario)
    {
        using var factory = new AuthTestFactory();
        var seed = await factory.SeedUsersAsync();
        using var client = factory.CreateClient();
        if (scenario != "anonymous") client.DefaultRequestHeaders.Authorization = new("Bearer", await LoginAsync(client, scenario == "teacher" ? seed.TeacherEmail : seed.AdminEmail, seed.Password));
        var bytes = scenario == "invalid-file" ? new byte[] { 1, 2, 3 } : Workbook(scenario == "duplicate" ? ["Nguyễn An", "  Nguyễn   An  "] : scenario == "missing-name" ? [""] : ["Nguyễn An"], scenario == "formula");
        var response = await client.PostAsync("/api/admin/students/import/confirm", Upload(bytes, scenario == "invalid-class" ? Guid.NewGuid() : seed.ClassId,
            scenario == "past-date" ? "2000-01-01" : null, scenario == "wrong-extension" ? "test.pdf" : "test.xlsx"));
        Assert.Equal(scenario == "anonymous" ? HttpStatusCode.Unauthorized : scenario == "teacher" ? HttpStatusCode.Forbidden : HttpStatusCode.BadRequest, response.StatusCode);
        using var scope = factory.Services.CreateScope();
        Assert.Equal(0, await scope.ServiceProvider.GetRequiredService<MealTraceDbContext>().Students.CountAsync());
        Assert.Equal(0, await scope.ServiceProvider.GetRequiredService<MealTraceDbContext>().StudentImportBatches.CountAsync());
    }

    [Fact]
    public void ReaderBoundsRowsAndRejectsMissingHeadersAndExternalSheets()
    {
        var reader = new StudentSpreadsheetReader();
        Assert.Throws<InvalidStudentSpreadsheetException>(() => reader.Read(new MemoryStream(Workbook(Enumerable.Range(1, 501).Select(i => $"Trẻ {i}").ToArray()))));
        Assert.Throws<InvalidStudentSpreadsheetException>(() => reader.Read(new MemoryStream(Workbook(["An"], header: "Không phải tên"))));
        Assert.Throws<InvalidStudentSpreadsheetException>(() => reader.Read(new MemoryStream(Workbook(["An"], external: true))));
        var valid = reader.Read(new MemoryStream(Workbook(["Trần Bình"])));
        Assert.Equal("Trần Bình", Assert.Single(valid.Rows).FullName);
        Assert.DoesNotContain("Ngày sinh", valid.IgnoredColumns);
        Assert.Equal(new DateOnly(2024, 1, 1), valid.Rows[0].DateOfBirth);
    }

    private static MultipartFormDataContent Upload(byte[] bytes, Guid classId, string? start = null, string name = "test.xlsx")
    {
        var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent(bytes), "file", name);
        form.Add(new StringContent(classId.ToString()), "classId");
        form.Add(new StringContent(start ?? DateTimeOffset.UtcNow.AddHours(7).ToString("yyyy-MM-dd")), "startDate");
        return form;
    }

    [Theory]
    [InlineData("0900000001", "0900000001")]
    [InlineData(" 0900000001 ", "0900000001")]
    [InlineData("", null)]
    public void ReaderPreviewsPhoneWithoutLosingLeadingZero(string phone, string? expected)
    {
        var row = Assert.Single(new StudentSpreadsheetReader().Read(new MemoryStream(Workbook(["An"], phone: phone))).Rows);
        Assert.Equal(expected, row.ParentPhoneNumber);
    }

    [Theory]
    [InlineData("31/02/2021", false, false, null)]
    [InlineData("03/12/2021", false, false, "2021-12-03")]
    [InlineData("45292", true, false, "2024-01-01")]
    [InlineData("43830", true, true, "2024-01-01")]
    [InlineData("60", true, false, null)]
    [InlineData("", false, false, null)]
    public void ReaderParsesVietnameseAndExcelDates(string birth, bool numeric, bool date1904, string? expected)
    {
        var row = Assert.Single(new StudentSpreadsheetReader().Read(new MemoryStream(Workbook(["An"], birth: birth,
            numericBirth: numeric, date1904: date1904))).Rows);
        Assert.Equal(expected, row.DateOfBirth?.ToString("yyyy-MM-dd"));
        if (expected is null && birth.Length > 0) Assert.NotNull(row.Error);
        else Assert.Null(row.Error);
    }

    [Theory]
    [InlineData("01/01/2099", "Nam")]
    [InlineData("31/02/2021", "Nữ")]
    [InlineData("01/01/2021", "Không rõ")]
    public async Task InvalidBirthOrGenderBlocksTheEntireImport(string birth, string gender)
    {
        using var factory = new AuthTestFactory();
        var seed = await factory.SeedUsersAsync();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", await LoginAsync(client, seed.AdminEmail, seed.Password));
        var response = await client.PostAsync("/api/admin/students/import/confirm", Upload(Workbook(["An", "Bình"], birth: birth, gender: gender), seed.ClassId));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var scope = factory.Services.CreateScope();
        Assert.Equal(0, await scope.ServiceProvider.GetRequiredService<MealTraceDbContext>().Students.CountAsync());
        Assert.Equal(0, await scope.ServiceProvider.GetRequiredService<MealTraceDbContext>().StudentImportBatches.CountAsync());
    }

    private static byte[] Workbook(string[] names, bool formula = false, string header = "Họ tên", bool external = false,
        string birth = "01/01/2024", string gender = "Nữ", bool numericBirth = false, bool date1904 = false, string phone = "0900000001")
    {
        XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        XElement Text(string address, string text) => new(ns + "c", new XAttribute("r", address), new XAttribute("t", "inlineStr"), new XElement(ns + "is", new XElement(ns + "t", text)));
        var rows = new List<XElement> { new(ns + "row", new XAttribute("r", 1), Text("A1", "DANH SÁCH LỚP KHÔNG DÙNG TÊN NÀY")),
            new(ns + "row", new XAttribute("r", 3), Text("A3", "STT"), Text("B3", header), Text("C3", "Ngày sinh"), Text("D3", "Giới tính"), Text("E3", "Số điện thoại liên hệ")) };
        for (var index = 0; index < names.Length; index++)
        {
            var n = index + 4;
            var nameCell = Text($"B{n}", names[index]);
            if (formula) nameCell.Add(new XElement(ns + "f", "CONCAT(\"An\",\"B\")"));
            var birthCell = numericBirth ? new XElement(ns + "c", new XAttribute("r", $"C{n}"), new XElement(ns + "v", birth)) : Text($"C{n}", birth);
            rows.Add(new XElement(ns + "row", new XAttribute("r", n), Text($"A{n}", (index + 1).ToString()), nameCell, birthCell, Text($"D{n}", gender), Text($"E{n}", phone)));
        }
        using var buffer = new MemoryStream();
        using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, true))
        {
            void Add(string path, string text) { using var writer = new StreamWriter(zip.CreateEntry(path).Open()); writer.Write(text); }
            Add("xl/workbook.xml", $"<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"><workbookPr date1904=\"{(date1904 ? 1 : 0)}\"/><sheets><sheet name=\"Danh sách\" sheetId=\"1\" r:id=\"rId1\"/></sheets></workbook>");
            Add("xl/_rels/workbook.xml.rels", $"<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Target=\"worksheets/sheet1.xml\"{(external ? " TargetMode=\"External\"" : "")}/></Relationships>");
            Add("xl/worksheets/sheet1.xml", new XDocument(new XElement(ns + "worksheet", new XElement(ns + "sheetData", rows))).ToString());
        }
        return buffer.ToArray();
    }
}

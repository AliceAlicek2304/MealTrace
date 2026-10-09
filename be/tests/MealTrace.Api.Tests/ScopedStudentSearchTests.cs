using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using MealTrace.Domain.Entities;
using MealTrace.Domain.Time;
using MealTrace.Infrastructure.Identity;
using MealTrace.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using static MealTrace.Api.Tests.AuthenticationTests;

namespace MealTrace.Api.Tests;

public sealed class ScopedStudentSearchTests
{
    [Fact]
    public Task TeacherSearchIsScopedAndPaginated() => VerifySearch(false);

    [PostgresFact]
    public Task TeacherSearchTranslatesFiltersOnPostgres() => VerifySearch(true);

    private static async Task VerifySearch(bool postgres)
    {
        using var factory = new AuthTestFactory(postgres: postgres);
        var seed = await factory.SeedUsersAsync();
        Guid outsideClass;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MealTraceDbContext>();
            var outside = new SchoolClass { Name = "Other", SchoolYear = "2026-2027" };
            db.Classes.Add(outside);
            outsideClass = outside.Id;
            db.TeacherAssignments.Add(new TeacherAssignment { UserId = seed.TeacherId, ClassId = seed.ClassId });
            var parent = new ApplicationUser { Id = Guid.NewGuid(), FullName = "Phụ huynh Nguyễn An", UserName = "guardian", PhoneNumber = "0901234567" };
            db.Users.Add(parent);
            var today = SchoolTime.Today(TimeProvider.System.GetUtcNow());
            for (var i = 0; i < 30; i++)
            {
                var child = new Student { FullName = $"Trẻ {i:D2}", ClassId = seed.ClassId,
                    Enrollments = [new Enrollment { ClassId = seed.ClassId, StartDate = today, RecordedAt = DateTimeOffset.UtcNow }] };
                db.Students.Add(child);
                if (i == 0) db.ParentStudents.Add(new ParentStudent { StudentId = child.Id, UserId = parent.Id });
            }
            db.Students.Add(new Student { FullName = "Private child", ClassId = outside.Id });
            // The pointer can already refer to a future class; access must follow today's enrollment.
            db.Students.Add(new Student { FullName = "Future transfer", ClassId = seed.ClassId,
                Enrollments = [new Enrollment { ClassId = outside.Id, StartDate = today, RecordedAt = DateTimeOffset.UtcNow }] });
            await db.SaveChangesAsync();
        }
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await LoginAsync(client, seed.TeacherEmail, seed.Password));
        var first = await client.GetFromJsonAsync<JsonElement>("/api/students/search?pageSize=20");
        Assert.Equal(30, first.GetProperty("total").GetInt32());
        Assert.Equal(20, first.GetProperty("items").GetArrayLength());
        var second = await client.GetFromJsonAsync<JsonElement>("/api/students/search?pageSize=20&page=2");
        Assert.Equal(10, second.GetProperty("items").GetArrayLength());
        var unlinked = await client.GetFromJsonAsync<JsonElement>("/api/students/search?parentStatus=UNLINKED");
        Assert.Equal(29, unlinked.GetProperty("total").GetInt32());
        Assert.All(unlinked.GetProperty("items").EnumerateArray(), row => Assert.Equal(0, row.GetProperty("parents").GetArrayLength()));
        var linked = await client.GetFromJsonAsync<JsonElement>("/api/students/search?parentStatus=LINKED&search=Trẻ%2000");
        Assert.Equal("Phụ huynh Nguyễn An", Assert.Single(Assert.Single(linked.GetProperty("items").EnumerateArray()).GetProperty("parents").EnumerateArray()).GetProperty("fullName").GetString());
        var outsidePage = await client.GetFromJsonAsync<JsonElement>($"/api/students/search?classId={outsideClass}");
        Assert.Equal(0, outsidePage.GetProperty("total").GetInt32());
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/admin/students")).StatusCode);
    }
}

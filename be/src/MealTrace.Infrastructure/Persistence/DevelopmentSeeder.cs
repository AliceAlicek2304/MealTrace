using MealTrace.Domain.Entities;
using MealTrace.Infrastructure.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using MealTrace.Domain.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace MealTrace.Infrastructure.Persistence;

public static class DevelopmentSeeder
{
    private static readonly (string Role, string Email, string FullName, string PhoneNumber)[] Accounts =
    [
        (RoleNames.Admin, "admin@demo.mealtrace.local", "Admin demo", "0900000001"),
        (RoleNames.Teacher, "teacher@demo.mealtrace.local", "Giáo viên demo", "0900000002"),
        (RoleNames.KitchenStaff, "kitchen@demo.mealtrace.local", "Bếp demo", "0900000003"),
        (RoleNames.Parent, "parent@demo.mealtrace.local", "Phụ huynh demo", "0900000006"),
    ];

    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MealTraceDbContext>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        await db.Database.MigrateAsync();

        foreach (var role in RoleNames.All)
        {
            if (!await roles.RoleExistsAsync(role))
            {
                var created = await roles.CreateAsync(new IdentityRole<Guid>(role));
                EnsureSucceeded(created, $"tạo role {role}");
            }
        }

        foreach (var account in Accounts)
        {
            var user = await users.FindByEmailAsync(account.Email);
            if (user is null)
            {
                var password = configuration[$"Seed:Passwords:{account.Role}"];
                if (string.IsNullOrWhiteSpace(password))
                    throw new InvalidOperationException($"Thiếu User Secret Seed:Passwords:{account.Role}. Chạy script be/scripts/setup-dev-secrets.ps1.");
                user = new ApplicationUser
                {
                    Id = Guid.NewGuid(),
                    UserName = account.Email,
                    Email = account.Email,
                    EmailConfirmed = true,
                    FullName = account.FullName,
                    IsActive = true,
                };
                EnsureSucceeded(await users.CreateAsync(user, password), $"tạo tài khoản {account.Role}");
            }
            // Fill missing demo phone numbers without overwriting admin corrections.
            if (string.IsNullOrWhiteSpace(user.PhoneNumber))
            {
                if (await db.Users.AnyAsync(x => x.Id != user.Id && x.PhoneNumber == account.PhoneNumber))
                    throw new InvalidOperationException($"Demo phone for {account.Role} is already assigned to another account.");
                user.PhoneNumber = account.PhoneNumber;
                user.PhoneNumberConfirmed = false;
                EnsureSucceeded(await users.UpdateAsync(user), $"add demo phone for {account.Role}");
            }
            if (!await users.IsInRoleAsync(user, account.Role))
                EnsureSucceeded(await users.AddToRoleAsync(user, account.Role), $"gán role {account.Role}");
        }

        // Scope records are seeded only for local exploration, never with real student data.
        const string demoCode = "HS-DEMO-0001";
        var demoStudent = await db.Students.FirstOrDefaultAsync(x => x.StudentCode == demoCode);
        // Once marked, find the original class through enrollment history even if names/class pointers changed.
        var originalClassId = demoStudent is null ? (Guid?)null : await db.Enrollments.Where(x => x.StudentId == demoStudent.Id)
            .OrderBy(x => x.StartDate).Select(x => (Guid?)x.ClassId).FirstOrDefaultAsync();
        var demoClass = originalClassId.HasValue ? await db.Classes.FindAsync(originalClassId.Value)
            : await db.Classes.FirstOrDefaultAsync(x => x.Name == "Lớp demo" && x.SchoolYear == "2026-2027");
        if (demoClass is null)
        {
            demoClass = new SchoolClass { Name = "Lớp demo", SchoolYear = "2026-2027" };
            db.Classes.Add(demoClass);
            await db.SaveChangesAsync();
        }
        demoStudent ??= await db.Students.FirstOrDefaultAsync(x => x.FullName == "Học sinh demo" && x.ClassId == demoClass.Id);
        if (demoStudent is null)
        {
            demoStudent = new Student { FullName = "Học sinh demo", ClassId = demoClass.Id, StudentCode = demoCode };
            db.Students.Add(demoStudent);
            await db.SaveChangesAsync();
        }
        else if (demoStudent.StudentCode != demoCode)
        {
            demoStudent.StudentCode = demoCode;
            await db.SaveChangesAsync();
        }
        var teacher = (await users.FindByEmailAsync("teacher@demo.mealtrace.local"))!;
        var parent = (await users.FindByEmailAsync("parent@demo.mealtrace.local"))!;
        if (!await db.TeacherAssignments.AnyAsync(x => x.UserId == teacher.Id && x.ClassId == demoClass.Id))
            db.TeacherAssignments.Add(new TeacherAssignment { UserId = teacher.Id, ClassId = demoClass.Id });
        if (!await db.ParentStudents.AnyAsync(x => x.UserId == parent.Id && x.StudentId == demoStudent.Id))
            db.ParentStudents.Add(new ParentStudent { UserId = parent.Id, StudentId = demoStudent.Id });
        await db.SaveChangesAsync();
    }

    private static void EnsureSucceeded(IdentityResult result, string operation)
    {
        if (!result.Succeeded)
            throw new InvalidOperationException($"Không thể {operation}: {string.Join("; ", result.Errors.Select(x => x.Description))}");
    }
}

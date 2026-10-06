using MealTrace.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace MealTrace.Infrastructure.Persistence.Repositories;

internal static class EnrollmentQueries
{
    public static IQueryable<Enrollment> OnDate(MealTraceDbContext db, DateOnly date) => db.Enrollments.AsNoTracking()
        .Where(x => x.StartDate <= date && (x.EndDate == null || x.EndDate > date));
}

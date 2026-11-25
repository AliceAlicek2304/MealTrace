using MealTrace.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace MealTrace.Infrastructure.Persistence.Repositories;

internal static class EnrollmentQueries
{
    public static IQueryable<Enrollment> OnDate(MealTraceDbContext db, DateOnly date) => db.Enrollments.AsNoTracking()
        .Where(x => x.StartDate <= date && (x.EndDate == null || x.EndDate > date));

    public static IQueryable<Enrollment> OnDateAsOf(MealTraceDbContext db, DateOnly date, DateTimeOffset asOf) => 
        db.Enrollments.AsNoTracking()
        .Where(x => x.StartDate <= date && 
                    x.RecordedAt <= asOf &&
                    (x.EndDate == null || x.EndDate > date || (x.EndDate == date && (x.EndRecordedAt == null || x.EndRecordedAt > asOf))));
}

namespace MealTrace.Api.Security;

public static class RoleNames
{
    public const string Admin = "ADMIN";
    public const string Teacher = "TEACHER";
    public const string KitchenStaff = "KITCHEN_STAFF";
    public const string Parent = "PARENT";

    public static readonly string[] All = [Admin, Teacher, KitchenStaff, Parent];
    public static readonly string[] MealStaff = [Admin, KitchenStaff];
    public static readonly string[] ReportReaders = [Admin];
}

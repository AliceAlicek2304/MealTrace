using System.Security.Cryptography;

namespace MealTrace.Api.Security;

public static class TemporaryPassword
{
    public static string Generate() => "Mt!9" + Convert.ToBase64String(RandomNumberGenerator.GetBytes(24))
        .TrimEnd('=').Replace('+', 'A').Replace('/', 'b');
}

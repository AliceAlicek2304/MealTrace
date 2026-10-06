using System.Text.RegularExpressions;
namespace MealTrace.Domain.Security;

public static class PhoneNumbers
{
    // Store Vietnamese mobile numbers consistently; accept 0..., +84... and 84....
    public static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 30) return null;
        var phone = Regex.Replace(value.Trim(), @"[\s().-]", "");
        if (phone.StartsWith("+84")) phone = "0" + phone[3..];
        else if (phone.StartsWith("84")) phone = "0" + phone[2..];
        return Regex.IsMatch(phone, @"^0[1-9][0-9]{8}$") ? phone : null;
    }
}

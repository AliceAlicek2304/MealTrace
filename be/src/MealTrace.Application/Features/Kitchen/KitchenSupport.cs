using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace MealTrace.Application.Features.Kitchen;

internal static class KitchenPaging
{
    /// <summary>Page 1-based, size 1..100 (default 25); skip never overflows.</summary>
    public static (int Page, int Size, int Skip) Normalize(int? page, int? pageSize)
    {
        var size = Math.Clamp(pageSize ?? 25, 1, 100);
        var pageNo = Math.Max(page ?? 1, 1);
        return (pageNo, size, (int)Math.Min((long)(pageNo - 1) * size, int.MaxValue));
    }
}

/// <summary>Canonical JSON and SHA-256 used for frozen snapshots and estimates: decimals stay decimals, never double.</summary>
internal static class KitchenJson
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web) { WriteIndented = false };

    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Options);

    public static T Deserialize<T>(string json) =>
        JsonSerializer.Deserialize<T>(json, Options) ?? throw new InvalidOperationException("Stored JSON could not be read.");

    public static string Sha256(string text) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
}

/// <summary>Allowed values shared by menus and reference ranges.</summary>
public static class MenuStates
{
    public const string Draft = "draft";
    public const string Submitted = "submitted";
    public const string Approved = "approved";
    public const string Rejected = "rejected";
    public const string Published = "published";
}

public static class ReferenceBasis
{
    public const string PerMeal = "perMeal";
    public const string Daily = "daily";
}

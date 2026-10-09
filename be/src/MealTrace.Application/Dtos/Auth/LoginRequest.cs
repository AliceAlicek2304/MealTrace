using System.ComponentModel.DataAnnotations;

namespace MealTrace.Application.Dtos.Auth;

public sealed record LoginRequest(
    [property: Required] string Identifier,
    [property: Required] string Password);

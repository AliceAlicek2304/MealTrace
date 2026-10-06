namespace MealTrace.Application.Abstractions;
/// <summary>The authenticated identity and roles supplied by the API boundary.</summary>
public interface ICurrentActor
{
    Guid? UserId { get; }
    bool IsInRole(string role);
}

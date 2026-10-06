using System.Security.Claims;
using MealTrace.Application.Abstractions;
namespace MealTrace.Api.Authentication;
internal sealed class HttpCurrentActor(IHttpContextAccessor accessor) : ICurrentActor
{
    public Guid? UserId => Guid.TryParse(accessor.HttpContext?.User.FindFirstValue("sub"), out var id) ? id : null;
    public bool IsInRole(string role) => accessor.HttpContext?.User.IsInRole(role) == true;
}

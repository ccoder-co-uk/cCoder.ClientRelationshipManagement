using cCoder.Security.Objects;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;

namespace cCoder.ClientRelationshipManagement.Api.Controllers;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
internal sealed class CrmUserAuthorizeAttribute : Attribute, IAuthorizationFilter
{
    public void OnAuthorization(AuthorizationFilterContext context)
    {
        ISSOAuthInfo auth = context.HttpContext.RequestServices.GetRequiredService<ISSOAuthInfo>();

        if (auth is null
            || string.IsNullOrWhiteSpace(auth.SSOUserId)
            || string.Equals(auth.SSOUserId, "Guest", StringComparison.OrdinalIgnoreCase))
        {
            context.Result = new UnauthorizedResult();
        }
    }
}

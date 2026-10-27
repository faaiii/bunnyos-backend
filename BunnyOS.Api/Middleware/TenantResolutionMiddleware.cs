using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using BunnyOS.Application.Interfaces;
using Microsoft.AspNetCore.Http;

namespace BunnyOS.Api.Middleware;

public class TenantResolutionMiddleware
{
    private readonly RequestDelegate _next;

    public TenantResolutionMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, ITenantContext tenantContext)
    {
        // 1. Extract tenant claim from JWT (Authoritative)
        var user = context.User;
        if (user.Identity?.IsAuthenticated == true)
        {
            var tenantClaim = user.FindFirst("tenant_id")?.Value;
            var userClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var roleClaim = user.FindFirst(ClaimTypes.Role)?.Value;

            if (Guid.TryParse(tenantClaim, out var tenantId))
            {
                Guid.TryParse(userClaim, out var userId);
                tenantContext.SetTenant(tenantId, userId == Guid.Empty ? null : userId, roleClaim);
            }
        }
        else
        {
            // Allow explicit X-Tenant-Id header for open public health/initialization if valid GUID
            if (context.Request.Headers.TryGetValue("X-Tenant-Id", out var headerTenant) &&
                Guid.TryParse(headerTenant.FirstOrDefault(), out var tenantId))
            {
                tenantContext.SetTenant(tenantId);
            }
        }

        await _next(context);
    }
}

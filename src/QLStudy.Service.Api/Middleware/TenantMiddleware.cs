using Microsoft.EntityFrameworkCore;
using QLStudy.Application.Common.Tenancy;
using QLStudy.Infrastructure.Data;

namespace QLStudy.Service.Api.Middleware
{
    public class TenantMiddleware
    {
        private readonly RequestDelegate _next;

        public TenantMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context, QLStudyDbContext dbContext, ICurrentTenant currentTenant)
        {
            var centerCode = context.Request.Headers["X-Center-Code"].FirstOrDefault();

            if (string.IsNullOrWhiteSpace(centerCode))
            {
                var host = context.Request.Host.Host;
                var parts = host.Split('.', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length > 2 && !host.Contains("localhost", StringComparison.OrdinalIgnoreCase))
                {
                    centerCode = parts[0];
                }
            }

            if (!string.IsNullOrWhiteSpace(centerCode))
            {
                var center = await dbContext.Centers
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(c => c.Code == centerCode && c.Status == "Active");

                if (center != null)
                {
                    currentTenant.SetTenant(center.Id, center.Code);
                }
            }

            await _next(context);
        }
    }
}


using DemoMVC.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace DemoMVC.Services
{
    public class DynamicPermissionFilter : IAsyncAuthorizationFilter
    {
        private readonly ApplicationDbContext _context;

        public DynamicPermissionFilter(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task OnAuthorizationAsync(
            AuthorizationFilterContext context)
        {
            var httpContext = context.HttpContext;
            var user = httpContext.User;

            // Không kiểm tra quyền động đối với các trang Identity.
            // Tránh vòng lặp chuyển hướng Login / AccessDenied.
            var path = httpContext.Request.Path;

            if (path.StartsWithSegments("/Identity"))
            {
                return;
            }

            // Cho phép Action có AllowAnonymous
            if (context.ActionDescriptor.EndpointMetadata
                .OfType<Microsoft.AspNetCore.Authorization.IAllowAnonymous>()
                .Any())
            {
                return;
            }

            // Chưa đăng nhập
            if (user.Identity?.IsAuthenticated != true)
            {
                context.Result = new ChallengeResult();
                return;
            }

            // Admin luôn có toàn quyền
            if (user.IsInRole("Admin"))
            {
                return;
            }

            var controller =
                context.RouteData.Values["controller"]?.ToString();

            var action =
                context.RouteData.Values["action"]?.ToString();

            if (string.IsNullOrWhiteSpace(controller) ||
                string.IsNullOrWhiteSpace(action))
            {
                context.Result = new ForbidResult();
                return;
            }

            // Lấy Role của người dùng
            var roleNames = user.Claims
                .Where(c => c.Type == ClaimTypes.Role)
                .Select(c => c.Value)
                .Distinct()
                .ToList();

            if (!roleNames.Any())
            {
                context.Result = new ForbidResult();
                return;
            }

            // Kiểm tra quyền trong database
            var hasPermission = await (
                from rp in _context.RolePermissions
                join role in _context.Roles
                    on rp.RoleId equals role.Id
                join permission in _context.Permissions
                    on rp.PermissionId equals permission.Id
                where roleNames.Contains(role.Name!)
                    && permission.Controller == controller
                    && permission.Action == action
                select rp
            ).AnyAsync();

            if (!hasPermission)
            {
                context.Result = new ForbidResult();
            }
        }
    }
}

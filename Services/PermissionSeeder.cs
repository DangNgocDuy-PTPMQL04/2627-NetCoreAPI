
using DemoMVC.Data;
using DemoMVC.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace DemoMVC.Services
{
    public class PermissionSeeder
    {
        private readonly ApplicationDbContext _context;
        private readonly IActionDescriptorCollectionProvider _actionProvider;
        private readonly RoleManager<IdentityRole> _roleManager;

        public PermissionSeeder(
            ApplicationDbContext context,
            IActionDescriptorCollectionProvider actionProvider,
            RoleManager<IdentityRole> roleManager)
        {
            _context = context;
            _actionProvider = actionProvider;
            _roleManager = roleManager;
        }

        public async Task SeedAsync()
        {
            // Lấy danh sách Action của các Controller trong dự án
            var actions = _actionProvider.ActionDescriptors.Items
                .OfType<ControllerActionDescriptor>()
                .Where(a =>
                    a.ControllerTypeInfo.Assembly ==
                    typeof(PermissionSeeder).Assembly)
                .Select(a => new
                {
                    Controller = a.ControllerName,
                    Action = a.ActionName
                })
                .Distinct()
                .ToList();

            // Lấy danh sách quyền đã tồn tại
            var existingPermissions = await _context.Permissions
                .ToListAsync();

            // Tạo quyền còn thiếu
            foreach (var action in actions)
            {
                bool exists = existingPermissions.Any(p =>
                    p.Controller == action.Controller &&
                    p.Action == action.Action);

                if (!exists)
                {
                    _context.Permissions.Add(new Permission
                    {
                        Controller = action.Controller,
                        Action = action.Action,
                        DisplayName = action.Controller +
                                      " - " + action.Action
                    });
                }
            }

            await _context.SaveChangesAsync();

            // Tìm Role Admin
            var adminRole = await _roleManager.FindByNameAsync("Admin");

            if (adminRole == null)
            {
                return;
            }

            // Lấy toàn bộ quyền
            var allPermissions = await _context.Permissions
                .ToListAsync();

            // Lấy quyền Admin đã có
            var existingAdminPermissions = await _context.RolePermissions
                .Where(rp => rp.RoleId == adminRole.Id)
                .Select(rp => rp.PermissionId)
                .ToListAsync();

            // Gán các quyền còn thiếu cho Admin
            foreach (var permission in allPermissions)
            {
                if (!existingAdminPermissions.Contains(permission.Id))
                {
                    _context.RolePermissions.Add(new RolePermission
                    {
                        RoleId = adminRole.Id,
                        PermissionId = permission.Id
                    });
                }
            }

            await _context.SaveChangesAsync();
        }
    }
}


using DemoMVC.Data;
using DemoMVC.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace DemoMVC.Controllers
{
    [Authorize(Roles = "Admin")]
    public class PermissionController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly RoleManager<IdentityRole> _roleManager;

        public PermissionController(
            ApplicationDbContext context,
            RoleManager<IdentityRole> roleManager)
        {
            _context = context;
            _roleManager = roleManager;
        }

        // Hiển thị giao diện phân quyền
        [HttpGet]
        public async Task<IActionResult> Index(string? roleId)
        {
            var roles = await _roleManager.Roles
                .OrderBy(r => r.Name)
                .ToListAsync();

            if (!roles.Any())
            {
                return View(new RolePermissionViewModel());
            }

            var selectedRole = roles.FirstOrDefault(r => r.Id == roleId)
                ?? roles.FirstOrDefault(r => r.Name == "User")
                ?? roles.First();

            var permissions = await _context.Permissions
                .OrderBy(p => p.Controller)
                .ThenBy(p => p.Action)
                .ToListAsync();

            var selectedIds = await _context.RolePermissions
                .Where(rp => rp.RoleId == selectedRole.Id)
                .Select(rp => rp.PermissionId)
                .ToListAsync();

            var model = new RolePermissionViewModel
            {
                RoleId = selectedRole.Id,
                RoleName = selectedRole.Name ?? string.Empty,
                IsAdmin = selectedRole.Name == "Admin",
                SelectedPermissionIds = selectedIds,

                Roles = roles.Select(r => new SelectListItem
                {
                    Text = r.Name,
                    Value = r.Id,
                    Selected = r.Id == selectedRole.Id
                }).ToList(),

                Groups = permissions
                    .GroupBy(p => p.Controller)
                    .Select(g => new PermissionGroupViewModel
                    {
                        Controller = g.Key,
                        Permissions = g.Select(p =>
                            new PermissionItemViewModel
                            {
                                Id = p.Id,
                                Action = p.Action,
                                DisplayName = p.DisplayName
                            }).ToList()
                    }).ToList()
            };

            return View(model);
        }

        // Lưu quyền cho Role
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Save(
            RolePermissionViewModel model)
        {
            var role = await _roleManager.FindByIdAsync(model.RoleId);

            if (role == null)
            {
                return NotFound("Không tìm thấy Role.");
            }

            // Admin luôn có toàn quyền, không cho chỉnh sửa tại đây
            if (role.Name == "Admin")
            {
                TempData["PermissionMessage"] =
                    "Role Admin luôn có toàn quyền.";

                return RedirectToAction(nameof(Index),
                    new { roleId = model.RoleId });
            }

            var selectedIds = (model.SelectedPermissionIds ?? new List<int>())
                .Distinct()
                .ToList();

            var validIds = await _context.Permissions
                .Where(p => selectedIds.Contains(p.Id))
                .Select(p => p.Id)
                .ToListAsync();

            if (validIds.Count != selectedIds.Count)
            {
                return BadRequest("Danh sách quyền không hợp lệ.");
            }

            var currentPermissions = await _context.RolePermissions
                .Where(rp => rp.RoleId == role.Id)
                .ToListAsync();

            // Xóa những quyền đã bỏ chọn
            var permissionsToRemove = currentPermissions
                .Where(rp => !selectedIds.Contains(rp.PermissionId))
                .ToList();

            _context.RolePermissions.RemoveRange(permissionsToRemove);

            // Thêm những quyền mới được chọn
            var currentIds = currentPermissions
                .Select(rp => rp.PermissionId)
                .ToHashSet();

            foreach (var permissionId in selectedIds)
            {
                if (!currentIds.Contains(permissionId))
                {
                    _context.RolePermissions.Add(new RolePermission
                    {
                        RoleId = role.Id,
                        PermissionId = permissionId
                    });
                }
            }

            await _context.SaveChangesAsync();

            TempData["PermissionMessage"] =
                "Cập nhật phân quyền thành công!";

            return RedirectToAction(nameof(Index),
                new { roleId = role.Id });
        }
    }
}

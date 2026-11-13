
using DemoMVC.Models;
using DemoMVC.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DemoMVC.Controllers;

[Authorize(Roles = "Admin")]
public class UsersController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;

    public UsersController(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager)
    {
        _userManager = userManager;
        _roleManager = roleManager;
    }

    // Danh sách tài khoản
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var users = await _userManager.Users
            .OrderBy(u => u.Email)
            .ToListAsync();

        var result = new List<UserRoleViewModel>();

        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);

            result.Add(new UserRoleViewModel
            {
                UserId = user.Id,
                Email = user.Email ?? "",
                FullName = user.FullName ?? "",
                Roles = roles.Select(r => new RoleSelectionViewModel
                {
                    RoleName = r,
                    IsSelected = true
                }).ToList()
            });
        }

        return View(result);
    }

    // Trang gán Role
    [HttpGet]
    public async Task<IActionResult> ManageRoles(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return NotFound();

        var user = await _userManager.FindByIdAsync(id);

        if (user == null)
            return NotFound();

        var userRoles = await _userManager.GetRolesAsync(user);

        var allRoles = await _roleManager.Roles
            .OrderBy(r => r.Name)
            .ToListAsync();

        var model = new UserRoleViewModel
        {
            UserId = user.Id,
            Email = user.Email ?? "",
            FullName = user.FullName ?? "",
            Roles = allRoles.Select(role =>
                new RoleSelectionViewModel
                {
                    RoleName = role.Name ?? "",
                    IsSelected = userRoles.Contains(role.Name ?? "")
                }).ToList()
        };

        return View(model);
    }

    // Lưu Role
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ManageRoles(UserRoleViewModel model)
    {
        var user = await _userManager.FindByIdAsync(model.UserId);

        if (user == null)
            return NotFound();

        var selectedRoles = model.Roles
            .Where(r => r.IsSelected)
            .Select(r => r.RoleName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        // Chỉ cho phép gán những Role đang tồn tại.
        foreach (var roleName in selectedRoles)
        {
            if (!await _roleManager.RoleExistsAsync(roleName))
            {
                TempData["Error"] = $"Role '{roleName}' không tồn tại.";
                return RedirectToAction(nameof(ManageRoles),
                    new { id = model.UserId });
            }
        }

        var currentRoles = await _userManager.GetRolesAsync(user);
        bool wasAdmin = currentRoles.Contains("Admin");
        bool willBeAdmin = selectedRoles.Contains(
            "Admin", StringComparer.OrdinalIgnoreCase);

        // Không cho tự thu hồi quyền Admin.
        if (user.Id == _userManager.GetUserId(User)
            && wasAdmin && !willBeAdmin)
        {
            TempData["Error"] =
                "Bạn không thể tự thu hồi quyền Admin của chính mình.";

            return RedirectToAction(nameof(ManageRoles),
                new { id = model.UserId });
        }

        // Không cho xóa quyền Admin cuối cùng của hệ thống.
        if (wasAdmin && !willBeAdmin)
        {
            var adminUsers = await _userManager.GetUsersInRoleAsync("Admin");

            if (adminUsers.Count <= 1)
            {
                TempData["Error"] =
                    "Không thể thu hồi quyền Admin cuối cùng của hệ thống.";

                return RedirectToAction(nameof(ManageRoles),
                    new { id = model.UserId });
            }
        }

        var rolesToRemove = currentRoles
            .Where(r => !selectedRoles.Contains(
                r, StringComparer.OrdinalIgnoreCase))
            .ToList();

        var rolesToAdd = selectedRoles
            .Where(r => !currentRoles.Contains(
                r, StringComparer.OrdinalIgnoreCase))
            .ToList();

        if (rolesToRemove.Count > 0)
        {
            var removeResult = await _userManager.RemoveFromRolesAsync(
                user, rolesToRemove);

            if (!removeResult.Succeeded)
            {
                TempData["Error"] = string.Join(
                    ", ",
                    removeResult.Errors.Select(e => e.Description));

                return RedirectToAction(nameof(ManageRoles),
                    new { id = model.UserId });
            }
        }

        if (rolesToAdd.Count > 0)
        {
            var addResult = await _userManager.AddToRolesAsync(
                user, rolesToAdd);

            if (!addResult.Succeeded)
            {
                TempData["Error"] = string.Join(
                    ", ",
                    addResult.Errors.Select(e => e.Description));

                return RedirectToAction(nameof(ManageRoles),
                    new { id = model.UserId });
            }
        }

        TempData["Success"] = "Cập nhật vai trò người dùng thành công.";

        return RedirectToAction(nameof(Index));
    }
}

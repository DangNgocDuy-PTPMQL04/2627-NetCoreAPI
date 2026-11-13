
using DemoMVC.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DemoMVC.Controllers;

[Authorize(Roles = "Admin")]
public class RoleController : Controller
{
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly UserManager<DemoMVC.Models.ApplicationUser> _userManager;

    public RoleController(
        RoleManager<IdentityRole> roleManager,
        UserManager<DemoMVC.Models.ApplicationUser> userManager)
    {
        _roleManager = roleManager;
        _userManager = userManager;
    }

    // Danh sách Role
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var roles = await _roleManager.Roles
            .OrderBy(r => r.Name)
            .ToListAsync();

        var result = new List<RoleViewModel>();

        foreach (var role in roles)
        {
            result.Add(new RoleViewModel
            {
                Id = role.Id,
                Name = role.Name ?? ""
            });
        }

        ViewBag.UserCounts = new Dictionary<string, int>();

        foreach (var role in roles)
        {
            var users = await _userManager.GetUsersInRoleAsync(
                role.Name ?? ""
            );

            ViewBag.UserCounts[role.Id] = users.Count;
        }

        return View(result);
    }

    // Trang thêm Role
    [HttpGet]
    public IActionResult Create()
    {
        return View();
    }

    // Xử lý thêm Role
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(RoleViewModel model)
    {
        model.Name = model.Name?.Trim() ?? "";

        if (!ModelState.IsValid)
            return View(model);

        if (await _roleManager.RoleExistsAsync(model.Name))
        {
            ModelState.AddModelError(
                "Name",
                "Vai trò này đã tồn tại."
            );

            return View(model);
        }

        var result = await _roleManager.CreateAsync(
            new IdentityRole(model.Name)
        );

        if (result.Succeeded)
        {
            TempData["Success"] = "Thêm vai trò thành công.";
            return RedirectToAction(nameof(Index));
        }

        foreach (var error in result.Errors)
            ModelState.AddModelError("", error.Description);

        return View(model);
    }

    // Trang sửa Role
    [HttpGet]
    public async Task<IActionResult> Edit(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return NotFound();

        var role = await _roleManager.FindByIdAsync(id);

        if (role == null)
            return NotFound();

        if (IsSystemRole(role.Name))
        {
            TempData["Error"] = "Không được sửa tên Role hệ thống.";
            return RedirectToAction(nameof(Index));
        }

        return View(new RoleViewModel
        {
            Id = role.Id,
            Name = role.Name ?? ""
        });
    }

    // Xử lý sửa Role
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(RoleViewModel model)
    {
        model.Name = model.Name?.Trim() ?? "";

        if (!ModelState.IsValid)
            return View(model);

        var role = await _roleManager.FindByIdAsync(model.Id);

        if (role == null)
            return NotFound();

        if (IsSystemRole(role.Name))
        {
            TempData["Error"] = "Không được sửa tên Role hệ thống.";
            return RedirectToAction(nameof(Index));
        }

        if (await _roleManager.Roles.AnyAsync(r =>
            r.Id != model.Id &&
            r.NormalizedName == model.Name.ToUpper()))
        {
            ModelState.AddModelError(
                "Name",
                "Tên vai trò đã được sử dụng."
            );

            return View(model);
        }

        role.Name = model.Name;

        var result = await _roleManager.UpdateAsync(role);

        if (result.Succeeded)
        {
            TempData["Success"] = "Cập nhật vai trò thành công.";
            return RedirectToAction(nameof(Index));
        }

        foreach (var error in result.Errors)
            ModelState.AddModelError("", error.Description);

        return View(model);
    }

    // Xóa Role
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return NotFound();

        var role = await _roleManager.FindByIdAsync(id);

        if (role == null)
            return NotFound();

        if (IsSystemRole(role.Name))
        {
            TempData["Error"] = "Không được xóa Role Admin hoặc User.";
            return RedirectToAction(nameof(Index));
        }

        var users = await _userManager.GetUsersInRoleAsync(
            role.Name ?? ""
        );

        if (users.Count > 0)
        {
            TempData["Error"] =
                $"Không thể xóa vai trò này vì đang có {users.Count} người dùng.";
            return RedirectToAction(nameof(Index));
        }

        var result = await _roleManager.DeleteAsync(role);

        if (result.Succeeded)
            TempData["Success"] = "Xóa vai trò thành công.";
        else
            TempData["Error"] = string.Join(
                ", ",
                result.Errors.Select(e => e.Description)
            );

        return RedirectToAction(nameof(Index));
    }

    private static bool IsSystemRole(string? name)
    {
        return string.Equals(name, "Admin",
                   StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, "User",
                   StringComparison.OrdinalIgnoreCase);
    }
}

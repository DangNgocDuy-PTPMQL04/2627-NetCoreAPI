
using System.ComponentModel.DataAnnotations;

namespace DemoMVC.ViewModels;

public class UserRoleViewModel
{
    public string UserId { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    public List<RoleSelectionViewModel> Roles { get; set; } = new();
}

public class RoleSelectionViewModel
{
    public string RoleName { get; set; } = string.Empty;

    public bool IsSelected { get; set; }
}

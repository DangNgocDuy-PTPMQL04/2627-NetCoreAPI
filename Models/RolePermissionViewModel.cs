
using Microsoft.AspNetCore.Mvc.Rendering;

namespace DemoMVC.Models
{
    public class RolePermissionViewModel
    {
        public string RoleId { get; set; } = string.Empty;

        public string RoleName { get; set; } = string.Empty;

        public bool IsAdmin { get; set; }

        public List<int> SelectedPermissionIds { get; set; } = new();

        public List<SelectListItem> Roles { get; set; } = new();

        public List<PermissionGroupViewModel> Groups { get; set; } = new();
    }

    public class PermissionGroupViewModel
    {
        public string Controller { get; set; } = string.Empty;

        public List<PermissionItemViewModel> Permissions { get; set; } = new();
    }

    public class PermissionItemViewModel
    {
        public int Id { get; set; }

        public string Action { get; set; } = string.Empty;

        public string DisplayName { get; set; } = string.Empty;
    }
}

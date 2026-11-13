
using System.ComponentModel.DataAnnotations;

namespace DemoMVC.ViewModels;

public class RoleViewModel
{
    public string Id { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập tên vai trò.")]
    [StringLength(50, ErrorMessage = "Tên vai trò không được vượt quá 50 ký tự.")]
    [Display(Name = "Tên vai trò")]
    public string Name { get; set; } = string.Empty;
}

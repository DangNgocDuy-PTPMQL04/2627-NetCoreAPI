
using System.ComponentModel.DataAnnotations;

namespace DemoMVC.Models
{
    public class Permission
    {
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Controller { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string Action { get; set; } = string.Empty;

        [StringLength(200)]
        public string DisplayName { get; set; } = string.Empty;
    }
}

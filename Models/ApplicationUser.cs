
using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace DemoMVC.Models
{
    public class ApplicationUser : IdentityUser
    {
        [StringLength(100)]
        public string? FullName { get; set; }
    }
}


using DemoMVC.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace DemoMVC.Data
{
    public class ApplicationDbContext
        : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(
            DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        // Các bảng nghiệp vụ hiện tại.
        public DbSet<Product> Products { get; set; }

        public DbSet<Faculty> Faculties { get; set; }

        public DbSet<Student> Students { get; set; }
    }
}

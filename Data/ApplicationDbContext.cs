
using DemoMVC.Models;
using Microsoft.AspNetCore.Identity;
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

        public DbSet<Product> Products { get; set; }

        public DbSet<Faculty> Faculties { get; set; }

        public DbSet<Student> Students { get; set; }

        public DbSet<Permission> Permissions { get; set; }

        public DbSet<RolePermission> RolePermissions { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // Cấu hình giá sản phẩm
            builder.Entity<Product>()
                .Property(p => p.Price)
                .HasPrecision(18, 2);

            // Khóa chính kết hợp RoleId và PermissionId
            builder.Entity<RolePermission>()
                .HasKey(rp => new { rp.RoleId, rp.PermissionId });

            // Liên kết RolePermission với Permission
            builder.Entity<RolePermission>()
                .HasOne(rp => rp.Permission)
                .WithMany()
                .HasForeignKey(rp => rp.PermissionId)
                .OnDelete(DeleteBehavior.Cascade);

            // Liên kết RolePermission với IdentityRole
            builder.Entity<RolePermission>()
                .HasOne<IdentityRole>()
                .WithMany()
                .HasForeignKey(rp => rp.RoleId)
                .OnDelete(DeleteBehavior.Cascade);

            // Mỗi Controller + Action chỉ có một quyền
            builder.Entity<Permission>()
                .HasIndex(p => new { p.Controller, p.Action })
                .IsUnique();
        }
    }
}

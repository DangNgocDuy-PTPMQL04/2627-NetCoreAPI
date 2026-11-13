
using DemoMVC.Data;
using DemoMVC.Models;
using DemoMVC.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Đăng ký bộ lọc phân quyền động
builder.Services.AddScoped<DynamicPermissionFilter>();

// Đăng ký dịch vụ khởi tạo danh sách quyền
builder.Services.AddScoped<PermissionSeeder>();

// MVC và bộ lọc phân quyền áp dụng cho các Controller Action
builder.Services.AddControllersWithViews(options =>
{
    options.Filters.AddService<DynamicPermissionFilter>();
});

// Razor Pages cho ASP.NET Core Identity
builder.Services.AddRazorPages();

// SQL Server
var connectionString =
    builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException(
        "Không tìm thấy DefaultConnection trong appsettings.json."
    );

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString)
);

// ASP.NET Core Identity
builder.Services
    .AddDefaultIdentity<ApplicationUser>(options =>
    {
        options.SignIn.RequireConfirmedEmail = true;

        options.Password.RequiredLength = 8;
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = true;

        options.Lockout.DefaultLockoutTimeSpan =
            TimeSpan.FromMinutes(5);

        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.AllowedForNewUsers = true;

        options.User.RequireUniqueEmail = true;
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

// Email sender
builder.Services.AddTransient<IEmailSender, EmailSender>();

// Cấu hình Cookie đăng nhập
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Identity/Account/Login";
    options.LogoutPath = "/Identity/Account/Logout";
    options.AccessDeniedPath = "/Identity/Account/AccessDenied";

    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
});

var app = builder.Build();

// Khởi tạo Role và tài khoản Admin mặc định
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;

    // Tạo Role Admin, User và tài khoản Admin
    await SeedIdentityAsync(services, builder.Configuration);

    // Tạo danh sách quyền từ các Controller Action
    var permissionSeeder =
        services.GetRequiredService<PermissionSeeder>();

    await permissionSeeder.SeedAsync();
}

// Cấu hình HTTP pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseRouting();

// Authentication phải đứng trước Authorization
app.UseAuthentication();

app.UseAuthorization();

// MVC
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}"
);

// Identity UI
app.MapRazorPages();

app.Run();


// ======================================================
// KHỞI TẠO ROLE VÀ TÀI KHOẢN ADMIN
// ======================================================

static async Task SeedIdentityAsync(
    IServiceProvider services,
    IConfiguration configuration)
{
    var roleManager =
        services.GetRequiredService<RoleManager<IdentityRole>>();

    var userManager =
        services.GetRequiredService<UserManager<ApplicationUser>>();

    // Các Role mặc định
    string[] roles = { "Admin", "User" };

    foreach (var role in roles)
    {
        if (!await roleManager.RoleExistsAsync(role))
        {
            var result = await roleManager.CreateAsync(
                new IdentityRole(role)
            );

            if (!result.Succeeded)
            {
                throw new Exception(
                    "Không thể tạo Role: " +
                    string.Join(", ",
                        result.Errors.Select(e => e.Description))
                );
            }
        }
    }

    // Đọc thông tin Admin từ appsettings.json
    var adminEmail = configuration["AdminSeed:Email"];
    var adminPassword = configuration["AdminSeed:Password"];

    if (string.IsNullOrWhiteSpace(adminEmail) ||
        string.IsNullOrWhiteSpace(adminPassword))
    {
        return;
    }

    var admin = await userManager.FindByEmailAsync(adminEmail);

    // Nếu chưa có Admin thì tạo mới
    if (admin == null)
    {
        admin = new ApplicationUser
        {
            UserName = adminEmail,
            Email = adminEmail,
            FullName = "Administrator",
            EmailConfirmed = true
        };

        var createResult = await userManager.CreateAsync(
            admin,
            adminPassword
        );

        if (!createResult.Succeeded)
        {
            throw new Exception(
                "Không thể tạo tài khoản Admin: " +
                string.Join(", ",
                    createResult.Errors.Select(e => e.Description))
            );
        }
    }

    // Gán Role Admin
    if (!await userManager.IsInRoleAsync(admin, "Admin"))
    {
        var roleResult = await userManager.AddToRoleAsync(
            admin,
            "Admin"
        );

        if (!roleResult.Succeeded)
        {
            throw new Exception(
                "Không thể gán quyền Admin: " +
                string.Join(", ",
                    roleResult.Errors.Select(e => e.Description))
            );
        }
    }

    // Xác nhận email cho tài khoản Admin mặc định
    if (!admin.EmailConfirmed)
    {
        admin.EmailConfirmed = true;
        await userManager.UpdateAsync(admin);
    }
}

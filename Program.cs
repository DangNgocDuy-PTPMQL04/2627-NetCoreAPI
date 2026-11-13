
using DemoMVC.Data;
using DemoMVC.Models;
using DemoMVC.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// MVC
builder.Services.AddControllersWithViews();

// Razor Pages cho Identity UI
builder.Services.AddRazorPages();

// SQL Server
var connectionString =
    builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException(
        "Không tìm thấy DefaultConnection."
    );

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString)
);

// ASP.NET Core Identity
builder.Services
    .AddDefaultIdentity<ApplicationUser>(options =>
    {
        // Bắt buộc xác nhận email trước khi đăng nhập.
        options.SignIn.RequireConfirmedEmail = true;

        // Cấu hình mật khẩu.
        options.Password.RequiredLength = 8;
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = true;

        // Khóa tài khoản sau nhiều lần đăng nhập sai.
        options.Lockout.DefaultLockoutTimeSpan =
            TimeSpan.FromMinutes(5);

        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.AllowedForNewUsers = true;

        // Không cho phép tên người dùng tùy ý.
        options.User.RequireUniqueEmail = true;
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

// Email sender
builder.Services.AddTransient<IEmailSender, EmailSender>();

// Cookie
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Identity/Account/Login";
    options.LogoutPath = "/Identity/Account/Logout";
    options.AccessDeniedPath = "/Identity/Account/AccessDenied";

    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
});

var app = builder.Build();

// Khởi tạo Role và tài khoản Admin mặc định.
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;

    await SeedIdentityAsync(services, builder.Configuration);
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseRouting();

// Phải đặt Authentication trước Authorization.
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


// Hàm tạo Role và Admin
static async Task SeedIdentityAsync(
    IServiceProvider services,
    IConfiguration configuration)
{
    var roleManager =
        services.GetRequiredService<RoleManager<IdentityRole>>();

    var userManager =
        services.GetRequiredService<UserManager<ApplicationUser>>();

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

    var adminEmail = configuration["AdminSeed:Email"];
    var adminPassword = configuration["AdminSeed:Password"];

    // Chưa cấu hình tài khoản mặc định thì bỏ qua việc tạo Admin.
    if (string.IsNullOrWhiteSpace(adminEmail) ||
        string.IsNullOrWhiteSpace(adminPassword))
    {
        return;
    }

    var admin = await userManager.FindByEmailAsync(adminEmail);

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

    // Tài khoản Admin mặc định đã được xác nhận email.
    if (!admin.EmailConfirmed)
    {
        admin.EmailConfirmed = true;
        await userManager.UpdateAsync(admin);
    }
}

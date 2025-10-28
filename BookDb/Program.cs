using BookDb.ExtendMethos;
using BookDb.Models;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.EntityFrameworkCore;
using BookDb.Repository.Interfaces;
using BookDb.Services.Implementations;
using BookDb.Services.Interfaces;
using BookDb.Repositories.Interfaces;
using BookDb.Repositories.Implementations;
using BookDb.Hubs;
using BookDb.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using BookDb.Middleware;

var builder = WebApplication.CreateBuilder(args);

// ====== Cấu hình dịch vụ (DI) ======
builder.Services.AddDbContext<AppDbContext>(options =>
{
    string connectString = builder.Configuration.GetConnectionString("DefaultConnection");
    options.UseSqlServer(connectString);
});

// Configure Identity
builder.Services.AddIdentity<User, IdentityRole>(options =>
{
    // Password settings
    options.Password.RequireDigit = false;
    options.Password.RequireLowercase = false;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
    options.Password.RequiredLength = 6;
    options.Password.RequiredUniqueChars = 0;

    // Lockout settings
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.AllowedForNewUsers = true;

    // User settings
    options.User.AllowedUserNameCharacters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._@+";
    options.User.RequireUniqueEmail = true;
})
.AddEntityFrameworkStores<AppDbContext>()
.AddDefaultTokenProviders();

// Configure JWT Settings
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
builder.Services.Configure<JwtSettings>(jwtSettings);

var key = Encoding.UTF8.GetBytes(jwtSettings["Secret"]);

// Configure JWT Authentication
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.SaveToken = true;
    options.RequireHttpsMetadata = false;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings["Issuer"],
        ValidAudience = jwtSettings["Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(key),
        ClockSkew = TimeSpan.Zero
    };

    // Configure JWT for SignalR
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var accessToken = context.Request.Query["access_token"];
            var path = context.HttpContext.Request.Path;

            if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/notify"))
            {
                context.Token = accessToken;
            }

            return Task.CompletedTask;
        }
    };
});

builder.Services.AddSingleton<FileStorageService>();

// Repositories
builder.Services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));
builder.Services.AddScoped<IDocumentRepository, DocumentRepository>();
builder.Services.AddScoped<IBookmarkRepository, BookmarkRepository>();
builder.Services.AddScoped<IDocumentPageRepository, DocumentPageRepository>();

// Services
builder.Services.AddScoped<IDocumentService, DocumentService>();
builder.Services.AddScoped<IBookmarkService, BookmarkService>();
builder.Services.AddScoped<IDocumentPageService, DocumentPageService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IJwtService, JwtService>();
builder.Services.AddScoped<IAuthService, AuthService>();

// Add Authorization Policies
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(Policies.RequireAdminRole, policy =>
        policy.RequireRole(Roles.Admin));

    options.AddPolicy(Policies.RequireManagerRole, policy =>
        policy.RequireRole(Roles.Admin, Roles.Manager));

    options.AddPolicy(Policies.RequireEditorRole, policy =>
        policy.RequireRole(Roles.Admin, Roles.Manager, Roles.Editor));

    options.AddPolicy(Policies.RequireContributorRole, policy =>
        policy.RequireRole(Roles.Admin, Roles.Manager, Roles.Editor, Roles.Contributor));

    options.AddPolicy(Policies.CanManageDocuments, policy =>
        policy.RequireRole(Roles.Admin, Roles.Manager, Roles.Editor));

    options.AddPolicy(Policies.CanEditDocuments, policy =>
        policy.RequireRole(Roles.Admin, Roles.Manager, Roles.Editor, Roles.Contributor));

    options.AddPolicy(Policies.CanViewDocuments, policy =>
        policy.RequireAuthenticatedUser());

    options.AddPolicy(Policies.CanManageUsers, policy =>
        policy.RequireRole(Roles.Admin, Roles.Manager));
});

builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();

// Add SignalR
builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = true;
    options.KeepAliveInterval = TimeSpan.FromSeconds(15);
    options.ClientTimeoutInterval = TimeSpan.FromSeconds(30);
});

// Cấu hình Razor View Engine
builder.Services.Configure<RazorViewEngineOptions>(options =>
{
    options.ViewLocationFormats.Add("/MyViews/{1}/{0}" + RazorViewEngine.ViewExtension);
});

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// ====== Build app ======
var app = builder.Build();

// ====== Seed roles and admin user ======
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = services.GetRequiredService<UserManager<User>>();

        await SeedRolesAndAdminUser(roleManager, userManager);
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while seeding the database");
    }
}

// ====== Middleware pipeline ======
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

// Tùy biến lỗi từ 400–599
app.AddStatusCodePage();

app.UseRouting();

// CORS (must be before Authentication)
app.UseCors("AllowAll");

// Use JWT Cookie Middleware (before Authentication)
app.UseJwtCookie();

app.UseAuthentication();
app.UseAuthorization();

// ====== Định tuyến ======
app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapRazorPages();

// Map SignalR hub
app.MapHub<NotificationHub>("/notify");

// ====== Chạy app ======
app.Run();

// ====== Helper method to seed roles and admin user ======
async Task SeedRolesAndAdminUser(RoleManager<IdentityRole> roleManager, UserManager<User> userManager)
{
    // Seed all roles
    string[] roleNames = Roles.GetAllRoles();
    foreach (var roleName in roleNames)
    {
        if (!await roleManager.RoleExistsAsync(roleName))
        {
            await roleManager.CreateAsync(new IdentityRole(roleName));
        }
    }

    // Seed admin user
    var adminEmail = "admin@bookdb.com";
    var adminUser = await userManager.FindByEmailAsync(adminEmail);

    if (adminUser == null)
    {
        var admin = new User
        {
            UserName = adminEmail,
            Email = adminEmail,
            FullName = "Administrator",
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
            EmailConfirmed = true
        };

        var result = await userManager.CreateAsync(admin, "Admin@123");
        if (result.Succeeded)
        {
            await userManager.AddToRoleAsync(admin, Roles.Admin);
        }
    }

    // Seed demo users for each role
    await SeedDemoUser(userManager, "manager@bookdb.com", "Manager@123", "Manager Demo", Roles.Manager);
    await SeedDemoUser(userManager, "editor@bookdb.com", "Editor@123", "Editor Demo", Roles.Editor);
    await SeedDemoUser(userManager, "contributor@bookdb.com", "Contributor@123", "Contributor Demo", Roles.Contributor);
    await SeedDemoUser(userManager, "user@bookdb.com", "User@123", "User Demo", Roles.User);
}

async Task SeedDemoUser(UserManager<User> userManager, string email, string password, string fullName, string role)
{
    var existingUser = await userManager.FindByEmailAsync(email);
    if (existingUser == null)
    {
        var user = new User
        {
            UserName = email,
            Email = email,
            FullName = fullName,
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
            EmailConfirmed = true
        };

        var result = await userManager.CreateAsync(user, password);
        if (result.Succeeded)
        {
            await userManager.AddToRoleAsync(user, role);
        }
    }
}
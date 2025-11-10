using BookDb.ExtendMethos;
using BookDb.Models;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.EntityFrameworkCore;
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

try
{
    // ====== Cấu hình dịch vụ (DI) ======
    string connectString = builder.Configuration.GetConnectionString("DefaultConnection");
    Console.WriteLine($"Connection String: {connectString}");

    builder.Services.AddDbContext<AppDbContext>(options =>
    {
        options.UseSqlServer(connectString);
        options.EnableSensitiveDataLogging(); // For debugging
        options.LogTo(Console.WriteLine, LogLevel.Information);
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

    builder.Services.AddHttpContextAccessor();
    builder.Services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();

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

    // Add Controllers and Views
    builder.Services.AddControllersWithViews()
        .AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
            options.JsonSerializerOptions.WriteIndented = true;
        });

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

    // Add Antiforgery
    builder.Services.AddAntiforgery(options =>
    {
        options.HeaderName = "X-CSRF-TOKEN";
        options.SuppressXFrameOptionsHeader = false;
    });

    // ====== Build app ======
    var app = builder.Build();

    Console.WriteLine("Application built successfully");

    // ====== Seed roles and admin user ======
    using (var scope = app.Services.CreateScope())
    {
        var services = scope.ServiceProvider;
        try
        {
            Console.WriteLine("Starting database seeding...");

            var context = services.GetRequiredService<AppDbContext>();

            // Check if database can be connected
            Console.WriteLine("Testing database connection...");
            if (await context.Database.CanConnectAsync())
            {
                Console.WriteLine("Database connection successful!");

                // Apply pending migrations
                Console.WriteLine("Checking for pending migrations...");
                var pendingMigrations = await context.Database.GetPendingMigrationsAsync();
                if (pendingMigrations.Any())
                {
                    Console.WriteLine($"Applying {pendingMigrations.Count()} pending migrations...");
                    await context.Database.MigrateAsync();
                    Console.WriteLine("Migrations applied successfully");
                }
                else
                {
                    Console.WriteLine("No pending migrations");
                }
            }
            else
            {
                Console.WriteLine("WARNING: Cannot connect to database!");
            }

            var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
            var userManager = services.GetRequiredService<UserManager<User>>();

            await SeedRolesAndAdminUser(roleManager, userManager);
            Console.WriteLine("Database seeding completed successfully");
        }
        catch (Exception ex)
        {
            var logger = services.GetRequiredService<ILogger<Program>>();
            logger.LogError(ex, "An error occurred while seeding the database");
            Console.WriteLine($"ERROR during seeding: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");

            // Don't throw - let app start anyway
        }
    }

    // ====== Middleware pipeline ======
    if (app.Environment.IsDevelopment())
    {
        app.UseDeveloperExceptionPage();
        Console.WriteLine("Running in Development mode");
    }
    else
    {
        app.UseExceptionHandler("/Home/Error");
        app.UseHsts();
    }

    if (app.Environment.IsDevelopment())
    {
        // Allow HTTP in development
        app.UseHttpsRedirection();
    }
    else
    {
        app.UseHsts();
        app.UseHttpsRedirection();
    }
    app.UseStaticFiles();

    // Tùy biến lỗi từ 400–599
    app.AddStatusCodePage();

    app.UseRouting();

    // CORS
    app.UseCors("AllowAll");

    // Use JWT Cookie Middleware
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

    Console.WriteLine("Application starting...");
    Console.WriteLine($"Environment: {app.Environment.EnvironmentName}");
    Console.WriteLine($"URLs: {string.Join(", ", builder.Configuration["Urls"]?.Split(';') ?? new[] { "http://localhost:5000" })}");

    // ====== Chạy app ======
    app.Run();
}
catch (Exception ex)
{
    Console.WriteLine("FATAL ERROR during application startup:");
    Console.WriteLine($"Message: {ex.Message}");
    Console.WriteLine($"Stack trace: {ex.StackTrace}");
    Console.WriteLine($"Inner exception: {ex.InnerException?.Message}");
    throw;
}

// ====== Helper method to seed roles and admin user ======
async Task SeedRolesAndAdminUser(RoleManager<IdentityRole> roleManager, UserManager<User> userManager)
{
    try
    {
        // Seed all roles
        string[] roleNames = Roles.GetAllRoles();
        foreach (var roleName in roleNames)
        {
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                Console.WriteLine($"Creating role: {roleName}");
                var result = await roleManager.CreateAsync(new IdentityRole(roleName));
                if (result.Succeeded)
                {
                    Console.WriteLine($"✓ Role created: {roleName}");
                }
                else
                {
                    Console.WriteLine($"✗ Failed to create role {roleName}: {string.Join(", ", result.Errors.Select(e => e.Description))}");
                }
            }
            else
            {
                Console.WriteLine($"Role already exists: {roleName}");
            }
        }

        // Seed admin user
        var adminEmail = "admin@bookdb.com";
        var adminUser = await userManager.FindByEmailAsync(adminEmail);

        if (adminUser == null)
        {
            Console.WriteLine("Creating admin user...");
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
                Console.WriteLine("✓ Admin user created successfully");
            }
            else
            {
                Console.WriteLine($"✗ Failed to create admin: {string.Join(", ", result.Errors.Select(e => e.Description))}");
            }
        }
        else
        {
            Console.WriteLine("Admin user already exists");
        }

        // Seed demo users
        await SeedDemoUser(userManager, "manager@bookdb.com", "Manager@123", "Manager Demo", Roles.Manager);
        await SeedDemoUser(userManager, "editor@bookdb.com", "Editor@123", "Editor Demo", Roles.Editor);
        await SeedDemoUser(userManager, "contributor@bookdb.com", "Contributor@123", "Contributor Demo", Roles.Contributor);
        await SeedDemoUser(userManager, "user@bookdb.com", "User@123", "User Demo", Roles.User);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error in SeedRolesAndAdminUser: {ex.Message}");
        throw;
    }
}

async Task SeedDemoUser(UserManager<User> userManager, string email, string password, string fullName, string role)
{
    try
    {
        var existingUser = await userManager.FindByEmailAsync(email);
        if (existingUser == null)
        {
            Console.WriteLine($"Creating demo user: {email}");
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
                Console.WriteLine($"✓ Demo user created: {email}");
            }
            else
            {
                Console.WriteLine($"✗ Failed to create user {email}: {string.Join(", ", result.Errors.Select(e => e.Description))}");
            }
        }
        else
        {
            Console.WriteLine($"Demo user already exists: {email}");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error creating demo user {email}: {ex.Message}");
    }
}
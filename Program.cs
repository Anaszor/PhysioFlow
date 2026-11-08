using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PhysioClinic.Data;
using PhysioClinic.Models;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// Configure Database (PostgreSQL or SQLite fallback for instant local preview)
var provider = builder.Configuration.GetValue<string>("DatabaseProvider") ?? "Sqlite";
var pgConn = builder.Configuration.GetConnectionString("PostgreSqlConnection");
var sqliteConn = builder.Configuration.GetConnectionString("SqliteConnection") ?? "Data Source=physioclinic.db";

builder.Services.AddDbContext<PhysioDbContext>(options =>
{
    if (provider.Equals("PostgreSql", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(pgConn))
    {
        options.UseNpgsql(pgConn);
    }
    else
    {
        options.UseSqlite(sqliteConn);
    }
});

// Configure ASP.NET Core Identity & Roles
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = false;
    options.Password.RequiredLength = 6;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
    options.Password.RequireLowercase = false;

    options.User.RequireUniqueEmail = true;
    options.SignIn.RequireConfirmedAccount = false;
})
.AddEntityFrameworkStores<PhysioDbContext>()
.AddDefaultTokenProviders();

// Configure Application Cookie
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.HttpOnly = true;
    options.Cookie.Name = ".PhysioFlow.Session";
    options.ExpireTimeSpan = TimeSpan.FromDays(7);
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.SlidingExpiration = true;
});

var app = builder.Build();

// Seed Database automatically on startup
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<PhysioDbContext>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        await DbInitializer.InitializeAsync(context, userManager, roleManager);
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while seeding the database.");
    }
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Dashboard}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();

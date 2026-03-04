using System.IO;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using SmartTrafficMonitor.Models;
using SmartTrafficMonitor.Services;

var builder = WebApplication.CreateBuilder(args);

// Services
builder.Services.AddControllersWithViews();

// Auth settings
builder.Services.Configure<AuthSettings>(builder.Configuration.GetSection("AuthSettings"));

// Audit log service
builder.Services.AddScoped<IAuditLogService, AuditLogService>();

// Event Impact Simulator service
builder.Services.AddScoped<IEventImpactProjectionService, EventImpactProjectionService>();

// 15 mins update hosted service
builder.Services.AddHostedService<TrafficUpdateHostedService>();

// Cookie authentication
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/Login";
    });

builder.Services.AddAuthorization();

// Database
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}
else
{
    app.UseDeveloperExceptionPage();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");


// 🔹 One-time CSV import on startup (Development only)
if (app.Environment.IsDevelopment())
{
    using (var scope = app.Services.CreateScope())
    {
        var services = scope.ServiceProvider;

        var context = services.GetRequiredService<ApplicationDbContext>();
        var audit = services.GetRequiredService<IAuditLogService>();

        var pedFolder = Path.Combine(app.Environment.ContentRootPath, "data", "Pedestrian Count");
        var vehFolder = Path.Combine(app.Environment.ContentRootPath, "data", "Vehicle Count");
        var cycFolder = Path.Combine(app.Environment.ContentRootPath, "data", "Cyclist Count");

        Console.WriteLine("[IMPORT] Starting CSV import...");
        Console.WriteLine($"[IMPORT] Pedestrian folder: {pedFolder}");
        Console.WriteLine($"[IMPORT] Vehicle folder:    {vehFolder}");
        Console.WriteLine($"[IMPORT] Cyclist folder:    {cycFolder}");

        var imported = CsvImportService.ImportFromFolders(context, audit, pedFolder, vehFolder, cycFolder);

        Console.WriteLine($"[IMPORT] Completed. Rows imported = {imported}");
    }
}

app.Run();
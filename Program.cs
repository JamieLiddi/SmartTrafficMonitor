using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using SmartTrafficMonitor.Models;
using SmartTrafficMonitor.Services;

var builder = WebApplication.CreateBuilder(args);

// Services
builder.Services.AddControllersWithViews();

//Auth settings
builder.Services.Configure<AuthSettings>(builder.Configuration.GetSection("AuthSettings"));

//Audit log service
builder.Services.AddScoped<IAuditLogService, AuditLogService>();

//15 mins update hosted service
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

app.Run();

using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using SmartTrafficMonitor.Models;
using Microsoft.AspNetCore.Authentication.Cookies;

var builder = WebApplication.CreateBuilder(args);

//Adding the services to the container.
builder.Services.AddControllersWithViews();

//Auth Foundation TO DO LATER!
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/AccessDenied";
    });
    builder.Services.AddAuthorization();

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

var app = builder.Build();

//Configuring the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}
else
{
    app.UseDeveloperExceptionPage();
}

//redirect HTTP -> HTTPS
app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseRouting();

//Enables authentication
app.UseAuthentication();

//enables authorization
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();

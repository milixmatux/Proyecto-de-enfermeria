using Enfermeria_app;
using Enfermeria_app.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.Cookies; // <- importante

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddSession();

builder.Services.AddDbContext<EnfermeriaContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("EnfermeriaContext")));

// ?? Configurar autenticación con cookies
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Cuenta/Login"; // Página a la que redirige si no está logeado
        options.LogoutPath = "/Cuenta/CerrarSesion";
        options.ExpireTimeSpan = TimeSpan.FromMinutes(30); // Tiempo de sesión
        options.SlidingExpiration = true;
    });

builder.Services.AddAuthorization();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseSession();

// ?? Middleware de autenticación y autorización (IMPORTANTE: antes de MapControllerRoute)
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Cuenta}/{action=Login}/{id?}");

app.Run();

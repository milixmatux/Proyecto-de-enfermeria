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

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("Profesor", p => p.RequireClaim("TipoUsuario", "Profesor"));
    options.AddPolicy("Estudiante", p => p.RequireClaim("TipoUsuario", "Estudiante"));
    options.AddPolicy("Funcionario", p => p.RequireClaim("TipoUsuario", "Funcionario"));
    options.AddPolicy("EstudianteFuncionario", p =>
        p.RequireAssertion(ctx =>
            ctx.User.HasClaim("TipoUsuario", "Estudiante") ||
            ctx.User.HasClaim("TipoUsuario", "Funcionario")));
    options.AddPolicy("EmergenciaProfesor", p => p.RequireClaim("TipoUsuario", "Profesor"));
});


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

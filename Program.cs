using Enfermeria_app;
using Enfermeria_app.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.Cookies;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddSession();

builder.Services.AddDbContext<EnfermeriaContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("EnfermeriaContext")));

// 🔐 AUTENTICACIÓN POR COOKIES
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Cuenta/Login";
        options.LogoutPath = "/Cuenta/CerrarSesion";
        options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
        options.SlidingExpiration = true;
    });

// AUTORIZACIÓN (ROLES/PERFILES)
builder.Services.AddAuthorization(options =>
{
    // Perfiles individuales
    options.AddPolicy("Estudiante", p => p.RequireClaim("TipoUsuario", "Estudiante"));
    options.AddPolicy("Funcionario", p => p.RequireClaim("TipoUsuario", "Funcionario"));
    options.AddPolicy("Profesor", p => p.RequireClaim("TipoUsuario", "Profesor"));
    options.AddPolicy("Consultorio", p => p.RequireClaim("TipoUsuario", "Consultorio"));
    options.AddPolicy("Administrativo", p => p.RequireClaim("TipoUsuario", "Administrativo"));

    // Perfiles combinados según funciones del sistema
    options.AddPolicy("EstudianteFuncionario", p =>
        p.RequireAssertion(ctx =>
            ctx.User.HasClaim("TipoUsuario", "Estudiante") ||
            ctx.User.HasClaim("TipoUsuario", "Funcionario")));

    // Acceso para quienes pueden gestionar emergencias
    options.AddPolicy("EmergenciaProfesor", p =>
    p.RequireClaim("TipoUsuario", "Profesor", "Administrativo"));

    // Acceso para quienes gestionan horarios (Consultorio)
    options.AddPolicy("GestionHorarios", p =>
        p.RequireClaim("TipoUsuario", "Consultorio"));

    // Acceso total (solo administrativo)
    options.AddPolicy("AdministrativoFull", p =>
        p.RequireClaim("TipoUsuario", "Administrativo"));
});



QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

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

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Cuenta}/{action=Login}/{id?}");

app.Run();
